using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Agent.Modules;

/// <summary>
/// Captures the local desktop and forwards keyboard/mouse events coming from the server.
/// The implementation intentionally mirrors MeshCentral semantics so the server can drive it easily.
/// </summary>
public sealed class DesktopModule : AgentModuleBase
{
    private static readonly IReadOnlyCollection<string> Actions = new[]
    {
        "desktopstart",
        "desktopstop",
        "desktopframe",
        "desktopmousemove",
        "desktopmouseclick",
        "desktopmousedown",
        "desktopmouseup",
        "desktopkeydown",
        "desktopkeyup",
        "desktopkeypress"
    };

    private readonly ConcurrentDictionary<string, DesktopSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public DesktopModule(ILogger<DesktopModule> logger) : base(logger)
    {
        // DesktopModule is now always registered
        // In service mode, will spawn user-session helper process (MeshCentral style)
    }

    public override string Name => "DesktopModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        if (!OperatingSystem.IsWindows())
        {
            await SendNotImplementedAsync(command, context, "Desktop sharing is only supported on Windows.").ConfigureAwait(false);
            return true;
        }

        switch (command.Action.ToLowerInvariant())
        {
            case "desktopstart":
                await HandleDesktopStartAsync(command, context).ConfigureAwait(false);
                return true;
            case "desktopstop":
                await HandleDesktopStopAsync(command, context).ConfigureAwait(false);
                return true;
            case "desktopframe":
                await HandleDesktopFrameAsync(command, context).ConfigureAwait(false);
                return true;
            case "desktopmousemove":
                await HandleMouseMoveAsync(command, context).ConfigureAwait(false);
                return true;
            case "desktopmouseclick":
                await HandleMouseClickAsync(command, context).ConfigureAwait(false);
                return true;
            case "desktopmousedown":
                await HandleMouseDownAsync(command, context).ConfigureAwait(false);
                return true;
            case "desktopmouseup":
                await HandleMouseUpAsync(command, context).ConfigureAwait(false);
                return true;
            case "desktopkeydown":
            case "desktopkeyup":
            case "desktopkeypress":
                await HandleKeyboardAsync(command, context).ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleDesktopStartAsync(AgentCommand command, AgentContext context)
    {
        var requestedSessionId = ResolveSessionId(command);
        var sessionId = !string.IsNullOrWhiteSpace(requestedSessionId) ? requestedSessionId! : Guid.NewGuid().ToString();

        int quality = 60;
        if (command.Payload.ValueKind == JsonValueKind.Object)
        {
            if (command.Payload.TryGetProperty("quality", out var qualityElement) && qualityElement.ValueKind == JsonValueKind.Number)
            {
                quality = qualityElement.GetInt32();
            }
        }

        if (_sessions.ContainsKey(sessionId))
        {
            await SendNotImplementedAsync(command, context, $"Desktop session '{sessionId}' already exists.").ConfigureAwait(false);
            return;
        }

        var session = new DesktopSession(sessionId, quality, Logger);
        _sessions[sessionId] = session;

        var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject
            {
                ["sessionId"] = sessionId,
                ["started"] = true,
                ["width"] = bounds.Width,
                ["height"] = bounds.Height,
                ["quality"] = quality
            })).ConfigureAwait(false);

        Logger.LogInformation("Desktop session started: {SessionId} (Quality={Quality})", sessionId, quality);
    }

    private async Task HandleDesktopStopAsync(AgentCommand command, AgentContext context)
    {
        var sessionId = ResolveSessionId(command) ?? string.Empty;
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.Dispose();
            Logger.LogInformation("Desktop session stopped: {SessionId}", sessionId);
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject
            {
                ["sessionId"] = sessionId,
                ["stopped"] = true
            })).ConfigureAwait(false);
    }

    private async Task HandleDesktopFrameAsync(AgentCommand command, AgentContext context)
    {
        var sessionId = ResolveSessionId(command) ?? string.Empty;
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            await SendNotImplementedAsync(command, context, $"Desktop session '{sessionId}' not found.").ConfigureAwait(false);
            return;
        }

        try
        {
            var frameData = session.CaptureFrame();
            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.CommandId,
                command.NodeId,
                command.SessionId,
                new JsonObject
                {
                    ["sessionId"] = sessionId,
                    ["frameBase64"] = Convert.ToBase64String(frameData),
                    ["size"] = frameData.Length
                })).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Desktop frame capture failed for session {SessionId}", sessionId);
            await SendNotImplementedAsync(command, context, $"Frame capture failed: {ex.Message}").ConfigureAwait(false);
        }
    }

    private async Task HandleMouseMoveAsync(AgentCommand command, AgentContext context)
    {
        if (!command.Payload.TryGetProperty("x", out var xElement) || !command.Payload.TryGetProperty("y", out var yElement))
        {
            await SendNotImplementedAsync(command, context, "Mouse move requires 'x' and 'y' coordinates.").ConfigureAwait(false);
            return;
        }

        var x = xElement.GetInt32();
        var y = yElement.GetInt32();
        
        // Get session from command or use first available
        var sessionId = ResolveSessionId(command);
        if (sessionId != null && _sessions.TryGetValue(sessionId, out var session) && session.IsHelperMode)
        {
            // Send to helper process
            session.SendToHelper($"MOUSEMOVE:{x},{y}");
        }
        else
        {
            // Fallback: direct call (won't work in service mode)
            NativeMethods.SetCursorPos(x, y);
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject
            {
                ["x"] = x,
                ["y"] = y,
                ["ack"] = true
            })).ConfigureAwait(false);
    }

    private async Task HandleMouseClickAsync(AgentCommand command, AgentContext context)
    {
        var button = command.Payload.TryGetProperty("button", out var buttonElement) && buttonElement.ValueKind == JsonValueKind.Number
            ? buttonElement.GetInt32()
            : 0;

        var sessionId = ResolveSessionId(command);
        if (sessionId != null && _sessions.TryGetValue(sessionId, out var session) && session.IsHelperMode)
        {
            session.SendToHelper($"MOUSECLICK:{button}");
        }
        else
        {
            uint downFlag = button switch
            {
                1 => NativeMethods.MOUSEEVENTF_RIGHTDOWN,
                2 => NativeMethods.MOUSEEVENTF_MIDDLEDOWN,
                _ => NativeMethods.MOUSEEVENTF_LEFTDOWN
            };

            uint upFlag = button switch
            {
                1 => NativeMethods.MOUSEEVENTF_RIGHTUP,
                2 => NativeMethods.MOUSEEVENTF_MIDDLEUP,
                _ => NativeMethods.MOUSEEVENTF_LEFTUP
            };

            NativeMethods.mouse_event(downFlag, 0, 0, 0, 0);
            NativeMethods.mouse_event(upFlag, 0, 0, 0, 0);
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject { ["ack"] = true, ["button"] = button })).ConfigureAwait(false);
    }

    private async Task HandleMouseDownAsync(AgentCommand command, AgentContext context)
    {
        var button = command.Payload.TryGetProperty("button", out var buttonElement) && buttonElement.ValueKind == JsonValueKind.Number
            ? buttonElement.GetInt32()
            : 0;

        var sessionId = ResolveSessionId(command);
        if (sessionId != null && _sessions.TryGetValue(sessionId, out var session) && session.IsHelperMode)
        {
            session.SendToHelper($"MOUSEDOWN:{button}");
        }
        else
        {
            uint flag = button switch
            {
                1 => NativeMethods.MOUSEEVENTF_RIGHTDOWN,
                2 => NativeMethods.MOUSEEVENTF_MIDDLEDOWN,
                _ => NativeMethods.MOUSEEVENTF_LEFTDOWN
            };

            NativeMethods.mouse_event(flag, 0, 0, 0, 0);
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject { ["ack"] = true })).ConfigureAwait(false);
    }

    private async Task HandleMouseUpAsync(AgentCommand command, AgentContext context)
    {
        var button = command.Payload.TryGetProperty("button", out var buttonElement) && buttonElement.ValueKind == JsonValueKind.Number
            ? buttonElement.GetInt32()
            : 0;

        var sessionId = ResolveSessionId(command);
        if (sessionId != null && _sessions.TryGetValue(sessionId, out var session) && session.IsHelperMode)
        {
            session.SendToHelper($"MOUSEUP:{button}");
        }
        else
        {
            uint flag = button switch
            {
                1 => NativeMethods.MOUSEEVENTF_RIGHTUP,
                2 => NativeMethods.MOUSEEVENTF_MIDDLEUP,
                _ => NativeMethods.MOUSEEVENTF_LEFTUP
            };

            NativeMethods.mouse_event(flag, 0, 0, 0, 0);
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject { ["ack"] = true })).ConfigureAwait(false);
    }

    private async Task HandleKeyboardAsync(AgentCommand command, AgentContext context)
    {
        if (!command.Payload.TryGetProperty("key", out var keyElement) || keyElement.ValueKind != JsonValueKind.Number)
        {
            await SendNotImplementedAsync(command, context, "Keyboard action requires 'key' code.").ConfigureAwait(false);
            return;
        }

        var keyCode = (byte)keyElement.GetInt32();
        var action = command.Action.ToLowerInvariant();

        var sessionId = ResolveSessionId(command);
        if (sessionId != null && _sessions.TryGetValue(sessionId, out var session) && session.IsHelperMode)
        {
            // Send to helper
            if (action == "desktopkeydown")
            {
                session.SendToHelper($"KEYDOWN:{keyCode}");
            }
            else if (action == "desktopkeyup")
            {
                session.SendToHelper($"KEYUP:{keyCode}");
            }
            else if (action == "desktopkeypress")
            {
                session.SendToHelper($"KEYPRESS:{keyCode}");
            }
        }
        else
        {
            // Fallback: direct call
            if (action == "desktopkeydown" || action == "desktopkeypress")
            {
                NativeMethods.keybd_event(keyCode, 0, 0, 0);
            }

            if (action == "desktopkeyup" || action == "desktopkeypress")
            {
                NativeMethods.keybd_event(keyCode, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
            }
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject { ["ack"] = true, ["key"] = keyCode })).ConfigureAwait(false);
    }

    public override async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        _sessions.Clear();
        await base.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class DesktopSession : IDisposable
    {
        private readonly string _sessionId;
        private readonly int _quality;
        private System.Diagnostics.Process? _helperProcess;
        private StreamWriter? _helperInput;
        private StreamReader? _helperOutput;
        private readonly bool _isServiceMode;
        private readonly ILogger? _logger;

        public bool IsHelperMode => _isServiceMode && _helperInput != null;

        public DesktopSession(string sessionId, int quality, ILogger? logger = null)
        {
            _sessionId = sessionId;
            _quality = Math.Clamp(quality, 10, 100);
            _isServiceMode = !Environment.UserInteractive;
            _logger = logger;
            
            _logger?.LogInformation("DesktopSession created: ServiceMode={IsService}, UserInteractive={UserInteractive}", 
                _isServiceMode, Environment.UserInteractive);
            
            // If running as service, start helper process in user session
            if (_isServiceMode)
            {
                _logger?.LogInformation("Starting DesktopHelper process...");
                StartHelperProcess();
            }
            else
            {
                _logger?.LogInformation("Console mode - will capture directly");
            }
        }

        private void StartHelperProcess()
        {
            try
            {
                // Find DesktopHelper.exe next to main agent executable
                var agentDir = AppContext.BaseDirectory;
                var helperPath = Path.Combine(agentDir, "DesktopHelper.exe");
                
                _logger?.LogInformation("Looking for DesktopHelper at: {Path}", helperPath);
                
                if (!File.Exists(helperPath))
                {
                    var msg = $"DesktopHelper not found at: {helperPath}";
                    _logger?.LogError(msg);
                    throw new FileNotFoundException(msg);
                }

                _logger?.LogInformation("DesktopHelper.exe found, getting active user session...");

                // Get active user session ID
                uint activeSessionId = GetActiveUserSessionId();
                if (activeSessionId == 0xFFFFFFFF)
                {
                    throw new Exception("No active user session found");
                }

                _logger?.LogInformation("Active session ID: {SessionId}, getting user token...", activeSessionId);

                // Get user token for the active session
                if (!NativeMethods.WTSQueryUserToken(activeSessionId, out IntPtr userToken))
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), 
                        "WTSQueryUserToken failed");
                }

                try
                {
                    _logger?.LogInformation("User token obtained, creating process in user session...");

                    // Create environment block for the user
                    IntPtr envBlock = IntPtr.Zero;
                    if (!NativeMethods.CreateEnvironmentBlock(out envBlock, userToken, false))
                    {
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
                            "CreateEnvironmentBlock failed");
                    }

                    try
                    {
                        // Create named pipe for communication
                        var pipeName = $"olmez_desktop_{_sessionId}";
                        
                        // Create pipe security that allows everyone to connect
                        var pipeSecurity = new System.IO.Pipes.PipeSecurity();
                        pipeSecurity.AddAccessRule(new System.IO.Pipes.PipeAccessRule(
                            new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid, null),
                            System.IO.Pipes.PipeAccessRights.FullControl,
                            System.Security.AccessControl.AccessControlType.Allow));
                        
                        // Service writes to "cmd" pipe (helper reads from it)
                        var pipeCmd = System.IO.Pipes.NamedPipeServerStreamAcl.Create(
                            pipeName + "_cmd",
                            System.IO.Pipes.PipeDirection.Out,
                            1,
                            System.IO.Pipes.PipeTransmissionMode.Byte,
                            System.IO.Pipes.PipeOptions.Asynchronous,
                            0,
                            0,
                            pipeSecurity);

                        // Service reads from "resp" pipe (helper writes to it)
                        var pipeResp = System.IO.Pipes.NamedPipeServerStreamAcl.Create(
                            pipeName + "_resp",
                            System.IO.Pipes.PipeDirection.In,
                            1,
                            System.IO.Pipes.PipeTransmissionMode.Byte,
                            System.IO.Pipes.PipeOptions.Asynchronous,
                            0,
                            0,
                            pipeSecurity);

                        // Prepare process creation
                        var si = new NativeMethods.STARTUPINFO();
                        si.cb = Marshal.SizeOf(si);
                        si.lpDesktop = "winsta0\\default";
                        si.dwFlags = 0;

                        var commandLine = $"\"{helperPath}\" {pipeName}";
                        var pi = new NativeMethods.PROCESS_INFORMATION();

                        // Launch process as user in their session with environment
                        bool created = NativeMethods.CreateProcessAsUser(
                            userToken,
                            null,
                            commandLine,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            false,
                            NativeMethods.CREATE_NO_WINDOW | NativeMethods.CREATE_UNICODE_ENVIRONMENT,
                            envBlock,
                            agentDir,
                            ref si,
                            out pi);

                        if (!created)
                        {
                            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
                                "CreateProcessAsUser failed");
                        }

                        _logger?.LogInformation("Process created successfully (PID: {Pid}), waiting for pipe connection...", pi.dwProcessId);

                        // Get process object and hook stderr
                        _helperProcess = System.Diagnostics.Process.GetProcessById(pi.dwProcessId);
                        
                        // Log if process exits unexpectedly
                        _helperProcess.EnableRaisingEvents = true;
                        _helperProcess.Exited += (s, e) =>
                        {
                            _logger?.LogError("Helper process exited unexpectedly! Exit code: {ExitCode}", 
                                _helperProcess?.ExitCode ?? -1);
                        };

                        // Wait for helper to connect to pipes with timeout
                        _logger?.LogInformation("Waiting for helper to connect...");
                        var connectTask1 = pipeCmd.WaitForConnectionAsync();
                        var connectTask2 = pipeResp.WaitForConnectionAsync();
                        
                        if (!Task.WaitAll(new[] { connectTask1, connectTask2 }, 10000))
                        {
                            throw new TimeoutException("Helper did not connect to pipes within 10 seconds");
                        }
                        
                        _logger?.LogInformation("Helper connected to both pipes");

                        _helperInput = new StreamWriter(pipeCmd) { AutoFlush = true };
                        _helperOutput = new StreamReader(pipeResp);

                        // Clean up handles
                        NativeMethods.CloseHandle(pi.hProcess);
                        NativeMethods.CloseHandle(pi.hThread);
                    }
                    finally
                    {
                        // Clean up environment block
                        if (envBlock != IntPtr.Zero)
                        {
                            NativeMethods.DestroyEnvironmentBlock(envBlock);
                        }
                    }

                    _logger?.LogInformation("Pipes connected, testing connection...");

                    // Test connection
                    _helperInput.WriteLine("PING");
                    var response = _helperOutput.ReadLine();
                    if (response != "PONG")
                    {
                        throw new Exception($"Helper process not responding correctly. Expected PONG, got: {response}");
                    }

                    _logger?.LogInformation("DesktopHelper connection test successful!");
                    _logger?.LogInformation("Check helper log at: %TEMP%\\DesktopHelper.log");
                }
                finally
                {
                    NativeMethods.CloseHandle(userToken);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start DesktopHelper: {Message}", ex.Message);
                _helperProcess?.Kill();
                _helperProcess = null;
            }
        }

        private static uint GetActiveUserSessionId()
        {
            IntPtr pSessionInfo = IntPtr.Zero;
            int count = 0;

            if (!NativeMethods.WTSEnumerateSessions(NativeMethods.WTS_CURRENT_SERVER_HANDLE, 0, 1, out pSessionInfo, out count))
            {
                return 0xFFFFFFFF;
            }

            try
            {
                int structSize = Marshal.SizeOf(typeof(NativeMethods.WTS_SESSION_INFO));
                IntPtr current = pSessionInfo;

                for (int i = 0; i < count; i++)
                {
                    var sessionInfo = Marshal.PtrToStructure<NativeMethods.WTS_SESSION_INFO>(current);
                    if (sessionInfo.State == NativeMethods.WTS_CONNECTSTATE_CLASS.WTSActive)
                    {
                        return (uint)sessionInfo.SessionId;
                    }
                    current = IntPtr.Add(current, structSize);
                }

                return 0xFFFFFFFF;
            }
            finally
            {
                NativeMethods.WTSFreeMemory(pSessionInfo);
            }
        }

        public void SendToHelper(string command)
        {
            if (_helperInput != null && _helperOutput != null)
            {
                try
                {
                    _helperInput.WriteLine(command);
                    _helperInput.Flush();
                    // Read ACK
                    var response = _helperOutput.ReadLine();
                    if (response != "ACK")
                    {
                        _logger?.LogWarning("Helper command '{Command}' got unexpected response: {Response}", command, response);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to send command to helper: {Command}", command);
                }
            }
        }

        public byte[] CaptureFrame()
        {
            // If service mode and helper is running, use helper
            if (_isServiceMode && _helperProcess != null && _helperInput != null && _helperOutput != null)
            {
                return CaptureFromHelper();
            }
            
            // Otherwise, capture directly (console mode)
            return CaptureDirectly();
        }

        private byte[] CaptureFromHelper()
        {
            try
            {
                // Send CAPTURE command to helper
                _helperInput!.WriteLine("CAPTURE");
                _helperInput.Flush();
                
                _logger?.LogInformation("Sent CAPTURE to helper, waiting for response...");
                
                // Read response
                var response = _helperOutput!.ReadLine();
                
                _logger?.LogInformation("Helper response received: {Response}", 
                    response?.Length > 100 ? response.Substring(0, 100) + "..." : response);
                
                if (response != null && response.StartsWith("IMAGE:"))
                {
                    var base64 = response.Substring(6);
                    _logger?.LogInformation("Base64 length: {Length}", base64.Length);
                    var bytes = Convert.FromBase64String(base64);
                    _logger?.LogInformation("Image decoded successfully: {Size} bytes", bytes.Length);
                    return bytes;
                }
                
                throw new Exception($"Invalid response from helper: {response}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CaptureFromHelper failed: {Message}", ex.Message);
                // Helper failed, draw error message
                return CreateErrorImage($"Helper process error: {ex.Message}");
            }
        }

        private byte[] CaptureDirectly()
        {
            var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            
            bool captured = false;
            
            try
            {
                // Method 1: Try CopyFromScreen (works if we successfully switched desktop)
                graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                captured = true;
            }
            catch
            {
                // Method 2: Try BitBlt from desktop DC
                try
                {
                    IntPtr desktopWindow = NativeMethods.GetDesktopWindow();
                    IntPtr desktopDC = NativeMethods.GetWindowDC(desktopWindow);
                    
                    if (desktopDC != IntPtr.Zero)
                    {
                        IntPtr memDC = NativeMethods.CreateCompatibleDC(desktopDC);
                        IntPtr hBitmap = NativeMethods.CreateCompatibleBitmap(desktopDC, bounds.Width, bounds.Height);
                        IntPtr oldBitmap = NativeMethods.SelectObject(memDC, hBitmap);
                        
                        bool success = NativeMethods.BitBlt(memDC, 0, 0, bounds.Width, bounds.Height, 
                            desktopDC, 0, 0, NativeMethods.SRCCOPY);
                        
                        if (success)
                        {
                            using var bmp = Image.FromHbitmap(hBitmap);
                            graphics.DrawImage(bmp, 0, 0, bounds.Width, bounds.Height);
                            captured = true;
                        }
                        
                        NativeMethods.SelectObject(memDC, oldBitmap);
                        NativeMethods.DeleteObject(hBitmap);
                        NativeMethods.DeleteDC(memDC);
                        NativeMethods.ReleaseDC(desktopWindow, desktopDC);
                    }
                }
                catch
                {
                    // Both methods failed
                }
            }
            
            if (!captured)
            {
                // Draw error message
                graphics.Clear(Color.FromArgb(30, 30, 30));
                using var font = new Font("Segoe UI", 14, FontStyle.Regular);
                using var brush = new SolidBrush(Color.White);
                var message = "Desktop Capture Failed\n\nService cannot access user desktop.\nPlease check service permissions.";
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                graphics.DrawString(message, font, brush, new RectangleF(0, 0, bounds.Width, bounds.Height), format);
            }

            using var ms = new MemoryStream();
            var encoder = GetEncoder(ImageFormat.Jpeg);
            var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)_quality);
            bitmap.Save(ms, encoder, encoderParameters);

            return ms.ToArray();
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();
            foreach (var codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return codecs[0];
        }

        private byte[] CreateErrorImage(string message)
        {
            var bounds = new Rectangle(0, 0, 1920, 1080);
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.Clear(Color.FromArgb(30, 30, 30));
            using var font = new Font("Segoe UI", 14, FontStyle.Regular);
            using var brush = new SolidBrush(Color.White);
            var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString($"Desktop Capture Failed\n\n{message}", font, brush, new RectangleF(0, 0, bounds.Width, bounds.Height), format);
            
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }

        public void Dispose()
        {
            try
            {
                if (_helperProcess != null)
                {
                    _helperInput?.WriteLine("EXIT");
                    _helperProcess.WaitForExit(1000);
                    if (!_helperProcess.HasExited)
                    {
                        _helperProcess.Kill();
                    }
                    _helperProcess.Dispose();
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private static class NativeMethods
    {
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const int SRCCOPY = 0x00CC0020;

        [Flags]
        public enum WindowStationAccessRights : uint
        {
            WINSTA_ALL_ACCESS = 0x37F
        }

        public enum DesktopAccessRights : uint
        {
            DESKTOP_NONE = 0,
            DESKTOP_READOBJECTS = 0x0001,
            DESKTOP_CREATEWINDOW = 0x0002,
            DESKTOP_CREATEMENU = 0x0004,
            DESKTOP_HOOKCONTROL = 0x0008,
            DESKTOP_JOURNALRECORD = 0x0010,
            DESKTOP_JOURNALPLAYBACK = 0x0020,
            DESKTOP_ENUMERATE = 0x0040,
            DESKTOP_WRITEOBJECTS = 0x0080,
            DESKTOP_SWITCHDESKTOP = 0x0100,
            GENERIC_ALL = DESKTOP_READOBJECTS | DESKTOP_CREATEWINDOW | DESKTOP_CREATEMENU |
                         DESKTOP_HOOKCONTROL | DESKTOP_JOURNALRECORD | DESKTOP_JOURNALPLAYBACK |
                         DESKTOP_ENUMERATE | DESKTOP_WRITEOBJECTS | DESKTOP_SWITCHDESKTOP
        }

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetProcessWindowStation();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetProcessWindowStation(IntPtr hWinSta);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr OpenWindowStation(string lpszWinSta, bool fInherit, WindowStationAccessRights dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool CloseWindowStation(IntPtr hWinSta);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr OpenDesktop(string lpszDesktop, int dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr OpenInputDesktop(int dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetThreadDesktop(uint dwThreadId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool CloseDesktop(IntPtr hDesktop);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, 
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int nStdHandle);

        // WTS and CreateProcessAsUser for spawning process in user session
        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

        [DllImport("userenv.dll", SetLastError = true)]
        public static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        public static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string? lpApplicationName,
            string? lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string? lpReserved;
            public string? lpDesktop;
            public string? lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        public const int STARTF_USESTDHANDLES = 0x00000100;
        public const uint CREATE_NEW_CONSOLE = 0x00000010;
        public const uint CREATE_NO_WINDOW = 0x08000000;
        public const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern bool WTSEnumerateSessions(
            IntPtr hServer,
            int Reserved,
            int Version,
            out IntPtr ppSessionInfo,
            out int pCount);

        [DllImport("wtsapi32.dll")]
        public static extern void WTSFreeMemory(IntPtr pMemory);

        [StructLayout(LayoutKind.Sequential)]
        public struct WTS_SESSION_INFO
        {
            public int SessionId;
            public IntPtr pWinStationName;
            public WTS_CONNECTSTATE_CLASS State;
        }

        public enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }

        public static IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;
    }

    private string? ResolveSessionId(AgentCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.SessionId))
        {
            return command.SessionId;
        }

        if (command.Payload.ValueKind == JsonValueKind.Object &&
            command.Payload.TryGetProperty("sessionId", out var sessionElement) &&
            sessionElement.ValueKind == JsonValueKind.String)
        {
            return sessionElement.GetString();
        }

        return null;
    }
}
