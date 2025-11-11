using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Agent.Modules;

public sealed class MessagingModule : AgentModuleBase
{
    private static readonly IReadOnlyCollection<string> Actions = new[]
    {
        "agentmsg",
        "messagebox",
        "notify",
        "toast",
        "chat",
        "getstate",
        "webrtcsdp",
        "webrtcice"
    };

    private readonly ConcurrentQueue<JsonObject> _agentMessages = new();
    private readonly ConcurrentQueue<JsonObject> _chatMessages = new();
    private readonly ConcurrentDictionary<string, JsonObject> _webrtcState = new(StringComparer.OrdinalIgnoreCase);
    private static Process? _notificationHelper;
    private static StreamWriter? _helperInput;
    private static StreamReader? _helperOutput;
    private static bool _helperStartAttempted = false;
    private static IAgentResponseWriter? _staticResponseWriter; // Helper response'lar için static referans
    private static string? _staticNodeId; // NodeId'yi de sakla (string format)

    public MessagingModule(ILogger<MessagingModule> logger)
        : base(logger)
    {
    }

    /// <summary>
    /// Server'a chat mesajı gönder (agent initiated)
    /// </summary>
    public async Task SendChatMessageToServerAsync(string message, string sender = "Agent")
    {
        if (_staticResponseWriter == null)
        {
            Logger.LogWarning("Cannot send chat message: ResponseWriter not set");
            return;
        }

        // CommandResult kullanmadan düz JSON gönder - commandId olmamalı
        var chatMessage = new JsonObject
        {
            ["action"] = "chatmessage",
            ["message"] = message,
            ["sender"] = sender,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O")
        };

        try
        {
            // Dummy CommandResult - ama action chatmessage olduğu için server bunu özel işleyecek
            await _staticResponseWriter.SendAsync(new CommandResult(
                "chatmessage",
                "00000000-0000-0000-0000-000000000000", // Dummy GUID - command lookup yapılmayacak
                _staticNodeId, // Device ID
                null,
                chatMessage
            )).ConfigureAwait(false);

            Logger.LogInformation("Chat message sent to server: [{Sender}] {Message}", sender, message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send chat message to server");
        }
    }

    public override string Name => "MessagingModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        // ResponseWriter ve NodeId'yi static field'a kaydet (helper response'lar için)
        _staticResponseWriter = context.ResponseWriter;
        _staticNodeId = command.NodeId?.ToString(); // Guid'i string'e çevir

        switch (command.Action.ToLowerInvariant())
        {
            case "agentmsg":
                await HandleAgentMessageAsync(command, context).ConfigureAwait(false);
                return true;
            case "messagebox":
            case "notify":
            case "toast":
                await HandleNotificationAsync(command, context).ConfigureAwait(false);
                return true;
            case "chat":
                await HandleChatAsync(command, context).ConfigureAwait(false);
                return true;
            case "getstate":
                await HandleGetStateAsync(command, context).ConfigureAwait(false);
                return true;
            case "webrtcsdp":
            case "webrtcice":
                await HandleWebRtcAsync(command, context).ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleAgentMessageAsync(AgentCommand command, AgentContext context)
    {
        var payload = command.Payload;
        var message = payload.TryGetProperty("message", out var msgElement) && msgElement.ValueKind == JsonValueKind.String
            ? msgElement.GetString()
            : null;
        var iconIndex = payload.TryGetProperty("iconIndex", out var iconElement) && iconElement.ValueKind == JsonValueKind.Number
            ? iconElement.GetInt32()
            : 0;

        var entry = new JsonObject
        {
            ["timestampUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["message"] = message,
            ["iconIndex"] = iconIndex
        };
        _agentMessages.Enqueue(entry);
        Logger.LogInformation("Agent message: {Message}", message);

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject
            {
                ["ack"] = true,
                ["messageCount"] = _agentMessages.Count
            })).ConfigureAwait(false);
    }

    private async Task HandleNotificationAsync(AgentCommand command, AgentContext context)
    {
        var payload = command.Payload;
        var title = payload.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String
            ? titleElement.GetString()
            : command.Action;
        var message = payload.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
            ? messageElement.GetString()
            : string.Empty;

        Logger.LogInformation("Notification ({Action}): {Title} - {Message}", command.Action, title, message);

        bool displayed = false;

        // Gerçek UI bildirimi göster (Windows'ta)
        if (OperatingSystem.IsWindows() && !string.IsNullOrEmpty(message))
        {
            try
            {
                // Service mode kontrolü - user session'da çalışmıyorsak helper'a gönder
                if (!Environment.UserInteractive)
                {
                    Logger.LogInformation("Service mode - forwarding notification to NotificationHelper");
                    displayed = await ForwardToNotificationHelperAsync(command.Action, title ?? "Agent", message).ConfigureAwait(false);
                }
                else
                {
                    switch (command.Action.ToLowerInvariant())
                    {
                        case "messagebox":
                            // MessageBox göster (blocking)
                            var mbResult = System.Windows.Forms.MessageBox.Show(
                                message,
                                title ?? "Agent Mesajı",
                                System.Windows.Forms.MessageBoxButtons.OK,
                                System.Windows.Forms.MessageBoxIcon.Information);
                            displayed = true;
                            break;

                        case "toast":
                        case "notify":
                            // Toast notification (non-blocking)
                            await ShowToastNotificationAsync(title ?? "Agent Bildirimi", message).ConfigureAwait(false);
                            displayed = true;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "UI bildirimi gösterilemedi");
            }
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject
            {
                ["ack"] = true,
                ["title"] = title,
                ["message"] = message,
                ["displayed"] = displayed,
                ["serviceMode"] = !Environment.UserInteractive
            })).ConfigureAwait(false);
    }

    private async Task<bool> ForwardToNotificationHelperAsync(string action, string title, string message)
    {
        // İlk kez çağrıldığında helper'ı başlat
        if (!_helperStartAttempted && OperatingSystem.IsWindows())
        {
            _helperStartAttempted = true;
            StartNotificationHelper();
            await Task.Delay(2000).ConfigureAwait(false); // Helper'ın başlaması için bekle
        }

        if (_helperInput == null)
        {
            Logger.LogWarning("NotificationHelper not connected");
            return false;
        }

        try
        {
            // Format: COMMAND:data
            string command = action.ToUpperInvariant() switch
            {
                "MESSAGEBOX" => $"MESSAGEBOX:{title}|{message}|OK",
                "TOAST" or "NOTIFY" => $"TOAST:{title}|{message}|Info",
                _ => $"TOAST:{title}|{message}|Info"
            };

            await _helperInput.WriteLineAsync(command).ConfigureAwait(false);
            Logger.LogInformation("Notification sent to helper: {Command}", command);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to send notification to helper");
            return false;
        }
    }

    private async Task<bool> ForwardChatToHelperAsync(string sender, string message)
    {
        // İlk kez çağrıldığında helper'ı başlat
        if (!_helperStartAttempted && OperatingSystem.IsWindows())
        {
            _helperStartAttempted = true;
            StartNotificationHelper();
            await Task.Delay(2000).ConfigureAwait(false);
        }

        if (_helperInput == null)
        {
            Logger.LogWarning("NotificationHelper not connected");
            return false;
        }

        try
        {
            // Format: CHAT:sender|message
            string command = $"CHAT:{sender}|{message}";
            await _helperInput.WriteLineAsync(command).ConfigureAwait(false);
            Logger.LogInformation("Chat sent to helper: {Sender} - {Message}", sender, message);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to send chat to helper");
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private void StartNotificationHelper()
    {
        try
        {
            var agentDir = AppContext.BaseDirectory;
            var helperPath = Path.Combine(agentDir, "NotificationHelper.exe");
            
            Logger.LogInformation("Looking for NotificationHelper at: {Path}", helperPath);
            
            if (!File.Exists(helperPath))
            {
                Logger.LogError("NotificationHelper not found at: {Path}", helperPath);
                return;
            }

            Logger.LogInformation("NotificationHelper.exe found, getting active user session...");

            // Get active user session ID
            uint activeSessionId = GetActiveUserSessionId();
            if (activeSessionId == 0xFFFFFFFF)
            {
                Logger.LogError("No active user session found");
                return;
            }

            Logger.LogInformation("Active session ID: {SessionId}, getting user token...", activeSessionId);

            // Get user token for the active session
            if (!NativeMethods.WTSQueryUserToken(activeSessionId, out IntPtr userToken))
            {
                Logger.LogError("WTSQueryUserToken failed: {Error}", Marshal.GetLastWin32Error());
                return;
            }

            try
            {
                Logger.LogInformation("User token obtained, creating process in user session...");

                // Create environment block for the user
                if (!NativeMethods.CreateEnvironmentBlock(out IntPtr envBlock, userToken, false))
                {
                    Logger.LogError("CreateEnvironmentBlock failed: {Error}", Marshal.GetLastWin32Error());
                    return;
                }

                try
                {
                    // Create named pipes for communication
                    var pipeName = "olmez_notification";
                    
                    var pipeSecurity = new System.IO.Pipes.PipeSecurity();
                    pipeSecurity.AddAccessRule(new System.IO.Pipes.PipeAccessRule(
                        new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid, null),
                        System.IO.Pipes.PipeAccessRights.FullControl,
                        System.Security.AccessControl.AccessControlType.Allow));
                    
                    // Service writes to "cmd" pipe (helper reads commands)
                    var pipeCmd = System.IO.Pipes.NamedPipeServerStreamAcl.Create(
                        pipeName + "_cmd",
                        System.IO.Pipes.PipeDirection.Out,
                        1,
                        System.IO.Pipes.PipeTransmissionMode.Byte,
                        System.IO.Pipes.PipeOptions.Asynchronous,
                        0, 0, pipeSecurity);

                    // Service reads from "resp" pipe (helper sends responses)
                    var pipeResp = System.IO.Pipes.NamedPipeServerStreamAcl.Create(
                        pipeName + "_resp",
                        System.IO.Pipes.PipeDirection.In,
                        1,
                        System.IO.Pipes.PipeTransmissionMode.Byte,
                        System.IO.Pipes.PipeOptions.Asynchronous,
                        0, 0, pipeSecurity);

                    // Prepare process creation
                    var si = new NativeMethods.STARTUPINFO();
                    si.cb = Marshal.SizeOf(si);
                    si.lpDesktop = "winsta0\\default";
                    si.dwFlags = 0;

                    var commandLine = $"\"{helperPath}\" {pipeName}";

                    // Launch process as user in their session
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
                        out var pi);

                    if (!created)
                    {
                        Logger.LogError("CreateProcessAsUser failed: {Error}", Marshal.GetLastWin32Error());
                        return;
                    }

                    Logger.LogInformation("NotificationHelper process created (PID: {Pid}), waiting for connection...", pi.dwProcessId);

                    _notificationHelper = Process.GetProcessById(pi.dwProcessId);
                    _notificationHelper.EnableRaisingEvents = true;
                    _notificationHelper.Exited += (s, e) =>
                    {
                        Logger.LogWarning("NotificationHelper exited unexpectedly! Exit code: {ExitCode}", 
                            _notificationHelper?.ExitCode ?? -1);
                        _helperInput = null;
                        _helperOutput = null;
                    };

                    // Wait for helper to connect
                    var connectTask1 = pipeCmd.WaitForConnectionAsync();
                    var connectTask2 = pipeResp.WaitForConnectionAsync();
                    
                    if (!Task.WaitAll(new[] { connectTask1, connectTask2 }, 10000))
                    {
                        Logger.LogError("NotificationHelper did not connect within 10 seconds");
                        _notificationHelper?.Kill();
                        return;
                    }
                    
                    Logger.LogInformation("NotificationHelper connected successfully");

                    _helperInput = new StreamWriter(pipeCmd) { AutoFlush = true };
                    _helperOutput = new StreamReader(pipeResp);

                    // Clean up handles
                    NativeMethods.CloseHandle(pi.hProcess);
                    NativeMethods.CloseHandle(pi.hThread);

                    Logger.LogInformation("NotificationHelper connected! Log: %TEMP%\\NotificationHelper.log");

                    // Helper'dan gelen mesajları dinle (ÖNCE listener'ı başlat)
                    _ = Task.Run(ListenToHelperResponsesAsync);
                    
                    // PING/PONG test (listener üzerinden handle edilecek)
                    Task.Delay(100).Wait(); // Listener'ın başlamasını bekle
                    _helperInput.WriteLine("PING");
                }
                finally
                {
                    if (envBlock != IntPtr.Zero)
                        NativeMethods.DestroyEnvironmentBlock(envBlock);
                }
            }
            finally
            {
                NativeMethods.CloseHandle(userToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start NotificationHelper");
            _notificationHelper?.Kill();
            _notificationHelper = null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static uint GetActiveUserSessionId()
    {
        IntPtr pSessionInfo = IntPtr.Zero;
        try
        {
            if (!NativeMethods.WTSEnumerateSessions(IntPtr.Zero, 0, 1, out pSessionInfo, out int count))
                return 0xFFFFFFFF;

            int dataSize = Marshal.SizeOf<NativeMethods.WTS_SESSION_INFO>();
            IntPtr current = pSessionInfo;

            for (int i = 0; i < count; i++)
            {
                var sessionInfo = Marshal.PtrToStructure<NativeMethods.WTS_SESSION_INFO>(current);
                
                if (sessionInfo.State == NativeMethods.WTS_CONNECTSTATE_CLASS.WTSActive)
                {
                    return sessionInfo.SessionId;
                }
                
                current = IntPtr.Add(current, dataSize);
            }

            return 0xFFFFFFFF;
        }
        finally
        {
            if (pSessionInfo != IntPtr.Zero)
                NativeMethods.WTSFreeMemory(pSessionInfo);
        }
    }

    private async Task ListenToHelperResponsesAsync()
    {
        Logger.LogInformation("ListenToHelperResponsesAsync started");
        try
        {
            while (_helperOutput != null && _notificationHelper != null && !_notificationHelper.HasExited)
            {
                Logger.LogInformation("Waiting for helper response...");
                var line = await _helperOutput.ReadLineAsync().ConfigureAwait(false);
                Logger.LogInformation("Helper response received: '{Response}'", line ?? "(null)");
                
                if (string.IsNullOrEmpty(line))
                    break;

                Logger.LogInformation("Helper response: {Response}", line);

                // PONG response (ping test)
                if (line.Equals("PONG", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInformation("NotificationHelper PING/PONG successful");
                    continue;
                }

                // CHAT_RESPONSE:message formatında gelecek
                if (line.StartsWith("CHAT_RESPONSE:", StringComparison.OrdinalIgnoreCase))
                {
                    var message = line.Substring("CHAT_RESPONSE:".Length);
                    var pcUserName = Environment.UserName; // PC kullanıcı adı

                    // Chat mesajını log'a kaydet
                    Logger.LogInformation("Chat message from PC user '{User}': {Message}", pcUserName, message);
                    
                    // Chat entry ekle
                    var chatEntry = new JsonObject
                    {
                        ["timestampUtc"] = DateTimeOffset.UtcNow.ToString("O"),
                        ["sender"] = pcUserName,
                        ["message"] = message,
                        ["direction"] = "outgoing"
                    };
                    _chatMessages.Enqueue(chatEntry);

                    // Server'a gönder
                    await SendChatMessageToServerAsync(message, pcUserName).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Helper response listening stopped");
        }
    }

    private async Task ShowToastNotificationAsync(string title, string message)
    {
        // PowerShell ile Windows Toast Notification göster
        var script = $@"
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null
$template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
$toastXml = [xml]$template.GetXml()
$toastXml.toast.visual.binding.text[0].AppendChild($toastXml.CreateTextNode('{title.Replace("'", "''")}')) > $null
$toastXml.toast.visual.binding.text[1].AppendChild($toastXml.CreateTextNode('{message.Replace("'", "''")}')) > $null
$xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$xml.LoadXml($toastXml.OuterXml)
$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('AgentHost').Show($toast)
";

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{script.Replace("\"", "`\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Toast notification gösterilemedi");
        }
    }

    private async Task HandleChatAsync(AgentCommand command, AgentContext context)
    {
        var payload = command.Payload;
        var sender = payload.TryGetProperty("sender", out var senderElement) && senderElement.ValueKind == JsonValueKind.String
            ? senderElement.GetString()
            : "Server";
        var message = payload.TryGetProperty("message", out var msgElement) && msgElement.ValueKind == JsonValueKind.String
            ? msgElement.GetString()
            : string.Empty;

        var chatEntry = new JsonObject
        {
            ["timestampUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["sender"] = sender,
            ["message"] = message
        };
        _chatMessages.Enqueue(chatEntry);
        Logger.LogInformation("Chat message from {Sender}: {Message}", sender, message);

        // Service mode ise NotificationHelper'a forward et
        bool displayed = false;
        if (OperatingSystem.IsWindows() && !Environment.UserInteractive && !string.IsNullOrEmpty(message))
        {
            Logger.LogInformation("Service mode - forwarding chat to NotificationHelper");
            displayed = await ForwardChatToHelperAsync(sender ?? "Server", message).ConfigureAwait(false);
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject
            {
                ["ack"] = true,
                ["queueLength"] = _chatMessages.Count,
                ["displayed"] = displayed
            })).ConfigureAwait(false);
    }

    private async Task HandleGetStateAsync(AgentCommand command, AgentContext context)
    {
        // Chat mesajlarını JSON array olarak döndür
        var messages = new JsonArray();
        foreach (var msg in _chatMessages)
        {
            messages.Add(msg);
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject
            {
                ["chatMessages"] = messages,
                ["messageCount"] = _chatMessages.Count
            })).ConfigureAwait(false);
    }

    private async Task HandleWebRtcAsync(AgentCommand command, AgentContext context)
    {
        var payload = JsonNode.Parse(command.Payload.GetRawText())!.AsObject();
        var key = command.Action.ToLowerInvariant();
        _webrtcState[key] = payload;
        Logger.LogDebug("WebRTC signal {Key}: {Payload}", key, payload.ToJsonString());

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject
            {
                ["ack"] = true,
                ["stateKeys"] = BuildStateKeyArray()
            })).ConfigureAwait(false);
    }

    private JsonArray BuildStateKeyArray()
    {
        var array = new JsonArray();
        foreach (var key in _webrtcState.Keys)
        {
            array.Add(key);
        }
        return array;
    }

    // Windows API interop
    [SupportedOSPlatform("windows")]
    private static class NativeMethods
    {
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

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct WTS_SESSION_INFO
        {
            public uint SessionId;
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
    }

    public override async ValueTask DisposeAsync()
    {
        // NotificationHelper'ı kapat
        await ShutdownNotificationHelperAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ShutdownNotificationHelperAsync()
    {
        if (_helperInput != null)
        {
            try
            {
                // SHUTDOWN komutu gönder
                await _helperInput.WriteLineAsync("SHUTDOWN").ConfigureAwait(false);
                await Task.Delay(500).ConfigureAwait(false); // Helper'ın kapanması için bekle
                Logger.LogInformation("Shutdown command sent to NotificationHelper");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to send shutdown command to NotificationHelper");
            }
        }

        // Stream'leri temizle
        _helperInput?.Dispose();
        _helperOutput?.Dispose();
        _helperInput = null;
        _helperOutput = null;

        // Process'i zorla kapat
        if (_notificationHelper != null && !_notificationHelper.HasExited)
        {
            try
            {
                _notificationHelper.Kill();
                _notificationHelper.WaitForExit(1000);
                Logger.LogInformation("NotificationHelper process terminated");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to kill NotificationHelper process");
            }
        }

        _notificationHelper?.Dispose();
        _notificationHelper = null;
        _helperStartAttempted = false;
    }
}
