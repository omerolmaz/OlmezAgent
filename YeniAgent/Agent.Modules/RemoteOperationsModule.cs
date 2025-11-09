
using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Agent.Modules;

public sealed class RemoteOperationsModule : AgentModuleBase
{
    private static readonly IReadOnlyCollection<string> Actions = new[]
    {
        "console",
        "power",
        "service",
        "ls",
        "download",
        "upload",
        "mkdir",
        "rm",
        "zip",
        "unzip",
        "openurl",
        "wallpaper",
        "kvmmode",
        "wakeonlan",
        "clipboardget",
        "clipboardset"
    };

    private readonly ConcurrentDictionary<string, ConsoleSession> _consoleSessions = new(StringComparer.OrdinalIgnoreCase);

    public RemoteOperationsModule(ILogger<RemoteOperationsModule> logger)
        : base(logger)
    {
    }

    public override string Name => "RemoteOperationsModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        switch (command.Action.ToLowerInvariant())
        {
            case "console":
                await HandleConsoleAsync(command, context).ConfigureAwait(false);
                return true;
            case "ls":
                await HandleListAsync(command, context).ConfigureAwait(false);
                return true;
            case "download":
                await HandleDownloadAsync(command, context).ConfigureAwait(false);
                return true;
            case "upload":
                await HandleUploadAsync(command, context).ConfigureAwait(false);
                return true;
            case "mkdir":
                await HandleMkdirAsync(command, context).ConfigureAwait(false);
                return true;
            case "rm":
                await HandleRemoveAsync(command, context).ConfigureAwait(false);
                return true;
            case "zip":
                await HandleZipAsync(command, context).ConfigureAwait(false);
                return true;
            case "unzip":
                await HandleUnzipAsync(command, context).ConfigureAwait(false);
                return true;
            case "service":
                await HandleServiceAsync(command, context).ConfigureAwait(false);
                return true;
            case "power":
                await HandlePowerAsync(command, context).ConfigureAwait(false);
                return true;
            case "openurl":
                await HandleOpenUrlAsync(command, context).ConfigureAwait(false);
                return true;
            case "wakeonlan":
                await HandleWakeOnLanAsync(command, context).ConfigureAwait(false);
                return true;
            case "clipboardget":
                await HandleClipboardGetAsync(command, context).ConfigureAwait(false);
                return true;
            case "clipboardset":
                await HandleClipboardSetAsync(command, context).ConfigureAwait(false);
                return true;
            case "wallpaper":
            case "kvmmode":
                await SendNotImplementedAsync(command, context, $"{command.Action} is not yet implemented.").ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleConsoleAsync(AgentCommand command, AgentContext context)
    {
        var parameters = GetParametersObject(command.Payload);
        var operation = (GetString(parameters, "operation", "Operation") ?? "start").ToLowerInvariant();
        var sessionId = GetString(parameters, "sessionId", "SessionId") ?? command.SessionId ?? throw new InvalidOperationException("console requires sessionId.");

        switch (operation)
        {
            case "start":
                if (_consoleSessions.ContainsKey(sessionId))
                {
                    await SendNotImplementedAsync(command, context, $"Console session '{sessionId}' already exists.").ConfigureAwait(false);
                    return;
                }

                var shell = GetString(parameters, "shell", "Shell") ?? "cmd.exe";
                var arguments = GetString(parameters, "arguments", "Args", "Parameters");

                var session = await ConsoleSession.StartAsync(shell, arguments).ConfigureAwait(false);
                _consoleSessions[sessionId] = session;

                await context.ResponseWriter.SendAsync(new CommandResult(
                    command.Action,
                    command.CommandId,
                    command.NodeId,
                    command.SessionId,
                    new JsonObject
                    {
                        ["sessionId"] = sessionId,
                        ["shell"] = shell,
                        ["started"] = true
                    })).ConfigureAwait(false);
                break;

            case "write":
                if (!_consoleSessions.TryGetValue(sessionId, out var writeSession))
                {
                    await SendNotImplementedAsync(command, context, $"Console session '{sessionId}' not found.").ConfigureAwait(false);
                    return;
                }

                var input = GetString(parameters, "input", "Input") ?? string.Empty;
                await writeSession.SendInputAsync(input).ConfigureAwait(false);
                await context.ResponseWriter.SendAsync(new CommandResult(
                    command.Action,
                    command.CommandId,
                    command.NodeId,
                    command.SessionId,
                    new JsonObject
                    {
                        ["sessionId"] = sessionId,
                        ["ack"] = true
                    })).ConfigureAwait(false);
                break;

            case "read":
                if (!_consoleSessions.TryGetValue(sessionId, out var readSession))
                {
                    await SendNotImplementedAsync(command, context, $"Console session '{sessionId}' not found.").ConfigureAwait(false);
                    return;
                }

                var output = readSession.ReadOutput();
                await context.ResponseWriter.SendAsync(new CommandResult(
                    command.Action,
                    command.CommandId,
                    command.NodeId,
                    command.SessionId,
                    new JsonObject
                    {
                        ["sessionId"] = sessionId,
                        ["output"] = output
                    })).ConfigureAwait(false);
                break;

            case "stop":
                if (_consoleSessions.TryRemove(sessionId, out var stopSession))
                {
                    await stopSession.DisposeAsync().ConfigureAwait(false);
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
                break;

            default:
                await SendNotImplementedAsync(command, context, $"Unknown console operation '{operation}'.").ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleListAsync(AgentCommand command, AgentContext context)
    {
        var parameters = GetParametersObject(command.Payload);
        Logger.LogInformation("Payload raw: {Payload}", command.Payload.GetRawText());
        Logger.LogInformation("Parameters object: {Params}", parameters.ToJsonString());
        
        var path = GetString(parameters, "path", "Path") ?? string.Empty;

        Logger.LogInformation("HandleListAsync: path = '{Path}'", path);

        var payload = new JsonObject();
        try
        {
            // Eğer path boş, "/" veya "root" ise, diskleri listele
            if (string.IsNullOrWhiteSpace(path) || path == "/" || path.Equals("root", StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("Diskleri listeliyorum...");
                var drives = new JsonArray();
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        var driveObj = new JsonObject
                        {
                            ["name"] = drive.Name,
                            ["type"] = "directory",
                            ["driveType"] = drive.DriveType.ToString(),
                            ["isReady"] = drive.IsReady
                        };

                        if (drive.IsReady)
                        {
                            driveObj["totalSize"] = drive.TotalSize;
                            driveObj["availableSpace"] = drive.AvailableFreeSpace;
                            driveObj["volumeLabel"] = drive.VolumeLabel ?? drive.Name;
                        }

                        drives.Add(driveObj);
                    }
                    catch
                    {
                        // Bazı sürücüler erişilemez olabilir, devam et
                    }
                }

                payload["entries"] = drives;
                payload["path"] = "root";
            }
            else
            {
                Logger.LogInformation("Dizin içeriği listeliyorum: '{Path}'", path);
                // Normal dizin listeleme
                var directory = new DirectoryInfo(path!);
                if (!directory.Exists)
                {
                    Logger.LogError("Dizin bulunamadı: '{Path}'", path);
                    throw new DirectoryNotFoundException($"Directory '{path}' was not found.");
                }

                Logger.LogInformation("Dizin var, içerik listeleniyor: {Count} klasör", directory.GetDirectories().Length);
                var entries = new JsonArray();
                
                // Önce dizinleri ekle
                foreach (var dir in directory.GetDirectories())
                {
                    try
                    {
                        entries.Add(new JsonObject
                        {
                            ["name"] = dir.Name,
                            ["type"] = "directory",
                            ["size"] = 0,
                            ["modifiedAt"] = dir.LastWriteTimeUtc.ToString("O")
                        });
                    }
                    catch
                    {
                        // Erişim hatası olan klasörleri atla
                    }
                }

                // Sonra dosyaları ekle
                foreach (var file in directory.GetFiles())
                {
                    try
                    {
                        entries.Add(new JsonObject
                        {
                            ["name"] = file.Name,
                            ["type"] = "file",
                            ["size"] = file.Length,
                            ["modifiedAt"] = file.LastWriteTimeUtc.ToString("O")
                        });
                    }
                    catch
                    {
                        // Erişim hatası olan dosyaları atla
                    }
                }

                payload["entries"] = entries;
                payload["path"] = path;
            }
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
        }

        payload["path"] = path ?? "root";

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: !payload.ContainsKey("error"),
            Error: payload.TryGetPropertyValue("error", out var err) ? err?.GetValue<string>() : null)).ConfigureAwait(false);
    }

    private async Task HandleDownloadAsync(AgentCommand command, AgentContext context)
    {
        var parameters = GetParametersObject(command.Payload);
        var path = GetString(parameters, "path", "Path");
        if (string.IsNullOrWhiteSpace(path))
        {
            await SendNotImplementedAsync(command, context, "download requires 'path'.").ConfigureAwait(false);
            return;
        }
        var payload = new JsonObject { ["path"] = path };
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, command.CancellationToken).ConfigureAwait(false);
            payload["contentBase64"] = Convert.ToBase64String(bytes);
            payload["length"] = bytes.Length;
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: !payload.ContainsKey("error"),
            Error: payload.TryGetPropertyValue("error", out var err) ? err?.GetValue<string>() : null)).ConfigureAwait(false);
    }

    private async Task HandleUploadAsync(AgentCommand command, AgentContext context)
    {
        var parameters = GetParametersObject(command.Payload);
        var path = GetString(parameters, "path", "Path");
        if (string.IsNullOrWhiteSpace(path))
        {
            await SendNotImplementedAsync(command, context, "upload requires 'path'.").ConfigureAwait(false);
            return;
        }
        var base64 = GetString(parameters, "contentBase64", "ContentBase64");
        if (string.IsNullOrWhiteSpace(base64))
        {
            await SendNotImplementedAsync(command, context, "upload requires 'contentBase64'.").ConfigureAwait(false);
            return;
        }

        var payload = new JsonObject { ["path"] = path };
        try
        {
            var bytes = Convert.FromBase64String(base64);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, bytes, command.CancellationToken).ConfigureAwait(false);
            payload["written"] = bytes.Length;
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: !payload.ContainsKey("error"),
            Error: payload.TryGetPropertyValue("error", out var err) ? err?.GetValue<string>() : null)).ConfigureAwait(false);
    }

    private async Task HandleServiceAsync(AgentCommand command, AgentContext context)
    {
        var parameters = GetParametersObject(command.Payload);
        var serviceName = GetString(parameters, "name", "serviceName", "ServiceName");
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            await SendNotImplementedAsync(command, context, "service requires 'name'.").ConfigureAwait(false);
            return;
        }

        var operation = (GetString(parameters, "operation", "action", "Operation", "Action") ?? "status").ToLowerInvariant();

        var payload = new JsonObject { ["name"] = serviceName, ["operation"] = operation };

        try
        {
            using var controller = new ServiceController(serviceName);
            switch (operation)
            {
                case "start":
                    if (controller.Status != ServiceControllerStatus.Running)
                    {
                        controller.Start();
                        controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    }
                    break;
                case "stop":
                    if (controller.Status != ServiceControllerStatus.Stopped)
                    {
                        controller.Stop();
                        controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                    break;
                case "restart":
                    controller.Stop();
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    controller.Start();
                    controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    break;
                case "status":
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported service operation '{operation}'.");
            }

            controller.Refresh();
            payload["status"] = controller.Status.ToString();
            payload["canStop"] = controller.CanStop;
            payload["serviceType"] = controller.ServiceType.ToString();
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: !payload.ContainsKey("error"),
            Error: payload.TryGetPropertyValue("error", out var err) ? err?.GetValue<string>() : null)).ConfigureAwait(false);
    }

    private async Task HandlePowerAsync(AgentCommand command, AgentContext context)
    {
        var parameters = GetParametersObject(command.Payload);
        var operation = (GetString(parameters, "operation", "action", "Operation", "Action") ?? "status").ToLowerInvariant();

        var payload = new JsonObject { ["operation"] = operation };
        try
        {
            switch (operation)
            {
                case "status":
                    payload["uptimeSeconds"] = Environment.TickCount64 / 1000;
                    payload["is64Bit"] = Environment.Is64BitOperatingSystem;
                    payload["osVersion"] = Environment.OSVersion.VersionString;
                    break;
                case "reboot":
                    await ExecuteProcessAsync(new ProcessStartInfo
                    {
                        FileName = "shutdown.exe",
                        Arguments = "/r /t 0",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }).ConfigureAwait(false);
                    break;
                case "shutdown":
                    await ExecuteProcessAsync(new ProcessStartInfo
                    {
                        FileName = "shutdown.exe",
                        Arguments = "/s /t 0",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }).ConfigureAwait(false);
                    break;
                case "logout":
                    await ExecuteProcessAsync(new ProcessStartInfo
                    {
                        FileName = "shutdown.exe",
                        Arguments = "/l",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown power operation '{operation}'.");
            }
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: !payload.ContainsKey("error"),
            Error: payload.TryGetPropertyValue("error", out var err) ? err?.GetValue<string>() : null)).ConfigureAwait(false);
    }

    private async Task HandleOpenUrlAsync(AgentCommand command, AgentContext context)
    {
        var parameters = GetParametersObject(command.Payload);
        var url = GetString(parameters, "url", "Url");
        if (string.IsNullOrWhiteSpace(url))
        {
            await SendNotImplementedAsync(command, context, "openurl requires 'url'.").ConfigureAwait(false);
            return;
        }

        var payload = new JsonObject { ["url"] = url };

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: !payload.ContainsKey("error"),
            Error: payload.TryGetPropertyValue("error", out var err) ? err?.GetValue<string>() : null)).ConfigureAwait(false);
    }

    private static ProcessStartInfo BuildInstallProcessStartInfo(string installerPath, bool silent, string? additionalArgs, string? workingDirectory)
    {
        var fileName = installerPath;
        var arguments = additionalArgs ?? string.Empty;

        if (installerPath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "msiexec.exe";
            var baseArgs = $"/i \"{installerPath}\"";
            if (silent)
            {
                baseArgs += " /qn /norestart";
            }

            arguments = string.IsNullOrWhiteSpace(arguments) ? baseArgs : $"{baseArgs} {arguments}";
        }
        else if (silent && string.IsNullOrWhiteSpace(arguments))
        {
            arguments = "/S /silent /quiet /qn /norestart";
        }

        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = silent
        };
    }

    private static async Task<ProcessResult> ExecuteProcessAsync(ProcessStartInfo startInfo)
    {
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdOut.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdErr.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync().ConfigureAwait(false);

        return new ProcessResult(process.ExitCode, stdOut.ToString(), stdErr.ToString());
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class ConsoleSession : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly StringBuilder _buffer = new();
        private readonly object _syncRoot = new();

        private ConsoleSession(Process process)
        {
            _process = process;
            _process.OutputDataReceived += (_, e) => Append(e.Data);
            _process.ErrorDataReceived += (_, e) => Append(e.Data);
        }

        private void Append(string? data)
        {
            if (data == null) return;
            lock (_syncRoot)
            {
                _buffer.AppendLine(data);
            }
        }

        public static async Task<ConsoleSession> StartAsync(string shell, string? arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = arguments ?? string.Empty,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start console session.");
            }

            var session = new ConsoleSession(process);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await Task.CompletedTask;
            return session;
        }

        public async Task SendInputAsync(string input)
        {
            if (_process.HasExited) return;
            await _process.StandardInput.WriteLineAsync(input).ConfigureAwait(false);
            await _process.StandardInput.FlushAsync().ConfigureAwait(false);
        }

        public string ReadOutput()
        {
            lock (_syncRoot)
            {
                var text = _buffer.ToString();
                _buffer.Clear();
                return text;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.StandardInput.WriteLine("exit");
                    _process.StandardInput.Flush();
                    await _process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                // ignore
            }

            _process.Dispose();
        }
    }

    private async Task HandleMkdirAsync(AgentCommand command, AgentContext context)
    {
        var parameters = GetParametersObject(command.Payload);
        var path = GetString(parameters, "path", "Path");
        if (string.IsNullOrWhiteSpace(path))
        {
            await SendNotImplementedAsync(command, context, "mkdir requires 'path'.").ConfigureAwait(false);
            return;
        }
        var payload = new JsonObject { ["path"] = path };

        try
        {
            Directory.CreateDirectory(path);
            payload["created"] = true;
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: !payload.ContainsKey("error"),
            Error: payload.TryGetPropertyValue("error", out var err) ? err?.GetValue<string>() : null)).ConfigureAwait(false);
    }

    private async Task HandleRemoveAsync(AgentCommand command, AgentContext context)
    {
        var parameters = GetParametersObject(command.Payload);
        var path = GetString(parameters, "path", "Path");
        if (string.IsNullOrWhiteSpace(path))
        {
            await SendNotImplementedAsync(command, context, "rm requires 'path'.").ConfigureAwait(false);
            return;
        }
        var recursiveValue = GetString(parameters, "recursive", "Recursive");
        var recursiveFlag = false;
        if (!string.IsNullOrWhiteSpace(recursiveValue))
        {
            _ = bool.TryParse(recursiveValue, out recursiveFlag);
        }

        var payload = new JsonObject
        {
            ["path"] = path,
            ["recursive"] = recursiveFlag
        };

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                payload["deleted"] = "file";
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, recursiveFlag);
                payload["deleted"] = "directory";
            }
            else
            {
                throw new FileNotFoundException($"Path not found: {path}");
            }
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: !payload.ContainsKey("error"),
            Error: payload.TryGetPropertyValue("error", out var err) ? err?.GetValue<string>() : null)).ConfigureAwait(false);
    }

    private async Task HandleZipAsync(AgentCommand command, AgentContext context)
    {
        var parameters = GetParametersObject(command.Payload);
        var source = GetString(parameters, "source", "Source");
        if (string.IsNullOrWhiteSpace(source))
        {
            await SendNotImplementedAsync(command, context, "zip requires 'source'.").ConfigureAwait(false);
            return;
        }

        var target = GetString(parameters, "target", "Target") ?? source + ".zip";

        var payload = new JsonObject { ["source"] = source, ["target"] = target };

        try
        {
            if (File.Exists(target))
            {
                File.Delete(target);
            }

            if (Directory.Exists(source))
            {
                ZipFile.CreateFromDirectory(source, target);
            }
            else if (File.Exists(source))
            {
                using var archive = ZipFile.Open(target, ZipArchiveMode.Create);
                archive.CreateEntryFromFile(source, Path.GetFileName(source));
            }
            else
            {
                throw new FileNotFoundException($"Source not found: {source}");
            }

            var fileInfo = new FileInfo(target);
            payload["created"] = true;
            payload["size"] = fileInfo.Length;
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: !payload.ContainsKey("error"),
            Error: payload.TryGetPropertyValue("error", out var err) ? err?.GetValue<string>() : null)).ConfigureAwait(false);
    }

    private async Task HandleUnzipAsync(AgentCommand command, AgentContext context)
    {
        var parameters = GetParametersObject(command.Payload);
        var source = GetString(parameters, "source", "Source");
        if (string.IsNullOrWhiteSpace(source))
        {
            await SendNotImplementedAsync(command, context, "unzip requires 'source'.").ConfigureAwait(false);
            return;
        }

        var target = GetString(parameters, "target", "Target")
            ?? Path.Combine(Path.GetDirectoryName(source) ?? Directory.GetCurrentDirectory(), Path.GetFileNameWithoutExtension(source));

        var payload = new JsonObject { ["source"] = source, ["target"] = target };

        try
        {
            if (!File.Exists(source))
            {
                throw new FileNotFoundException($"Archive not found: {source}");
            }

            Directory.CreateDirectory(target);
            ZipFile.ExtractToDirectory(source, target, overwriteFiles: true);
            payload["extracted"] = true;
            payload["fileCount"] = Directory.GetFiles(target, "*", SearchOption.AllDirectories).Length;
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: !payload.ContainsKey("error"),
            Error: payload.TryGetPropertyValue("error", out var err) ? err?.GetValue<string>() : null)).ConfigureAwait(false);
    }

    private async Task HandleWakeOnLanAsync(AgentCommand command, AgentContext context)
    {
        var parameters = GetParametersObject(command.Payload);
        var macAddress = GetString(parameters, "mac", "macAddress", "Mac", "MacAddress");
        if (string.IsNullOrWhiteSpace(macAddress))
        {
            await SendNotImplementedAsync(command, context, "wakeonlan requires 'mac' address.").ConfigureAwait(false);
            return;
        }

        var payload = new JsonObject { ["mac"] = macAddress };

        try
        {
            var macBytes = ParseMacAddress(macAddress);
            var magicPacket = BuildMagicPacket(macBytes);

            using var client = new UdpClient();
            client.EnableBroadcast = true;
            var endpoint = new IPEndPoint(IPAddress.Broadcast, 9);
            await client.SendAsync(magicPacket, magicPacket.Length, endpoint).ConfigureAwait(false);

            payload["sent"] = true;
            payload["packetSize"] = magicPacket.Length;
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: !payload.ContainsKey("error"),
            Error: payload.TryGetPropertyValue("error", out var err) ? err?.GetValue<string>() : null)).ConfigureAwait(false);
    }

    private static byte[] ParseMacAddress(string mac)
    {
        var cleanMac = mac.Replace(":", "").Replace("-", "").Replace(" ", "");
        if (cleanMac.Length != 12)
        {
            throw new ArgumentException("Invalid MAC address format");
        }

        var bytes = new byte[6];
        for (int i = 0; i < 6; i++)
        {
            bytes[i] = Convert.ToByte(cleanMac.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    private static byte[] BuildMagicPacket(byte[] macAddress)
    {
        var packet = new byte[102];

        // 6 bytes of 0xFF
        for (int i = 0; i < 6; i++)
        {
            packet[i] = 0xFF;
        }

        // MAC address repeated 16 times
        for (int i = 0; i < 16; i++)
        {
            Array.Copy(macAddress, 0, packet, 6 + (i * 6), 6);
        }

        return packet;
    }

    private async Task HandleClipboardGetAsync(AgentCommand command, AgentContext context)
    {
        var payload = new JsonObject();

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var text = System.Windows.Forms.Clipboard.GetText();
                payload["content"] = text;
                payload["type"] = "text";
            }
            else
            {
                payload["error"] = "Clipboard operations are only supported on Windows";
            }
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: !payload.ContainsKey("error"),
            Error: payload.TryGetPropertyValue("error", out var err) ? err?.GetValue<string>() : null)).ConfigureAwait(false);
    }

    private async Task HandleClipboardSetAsync(AgentCommand command, AgentContext context)
    {
        var parameters = GetParametersObject(command.Payload);
        var content = GetString(parameters, "content", "Content");
        if (string.IsNullOrWhiteSpace(content))
        {
            await SendNotImplementedAsync(command, context, "clipboardset requires 'content'.").ConfigureAwait(false);
            return;
        }

        var payload = new JsonObject { ["content"] = content };

        try
        {
            if (OperatingSystem.IsWindows())
            {
                System.Windows.Forms.Clipboard.SetText(content);
                payload["set"] = true;
            }
            else
            {
                payload["error"] = "Clipboard operations are only supported on Windows";
            }
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: !payload.ContainsKey("error"),
            Error: payload.TryGetPropertyValue("error", out var err) ? err?.GetValue<string>() : null)).ConfigureAwait(false);
    }

    private static JsonObject GetParametersObject(JsonElement payload)
    {
        JsonObject? TryParse(JsonElement element)
        {
            try
            {
                return JsonNode.Parse(element.GetRawText())?.AsObject();
            }
            catch
            {
                return null;
            }
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            if (payload.TryGetProperty("parameters", out var parametersElement))
            {
                if (parametersElement.ValueKind == JsonValueKind.Object)
                {
                    var parsed = TryParse(parametersElement);
                    if (parsed != null)
                    {
                        return parsed;
                    }
                }

                if (parametersElement.ValueKind == JsonValueKind.String)
                {
                    var raw = parametersElement.GetString();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        try
                        {
                            return JsonNode.Parse(raw)?.AsObject() ?? new JsonObject();
                        }
                        catch
                        {
                            // ignore and fall back
                        }
                    }
                }
            }

            var fallback = TryParse(payload);
            if (fallback != null)
            {
                return fallback;
            }
        }
        else if (payload.ValueKind == JsonValueKind.String)
        {
            var raw = payload.GetString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    return JsonNode.Parse(raw)?.AsObject() ?? new JsonObject();
                }
                catch
                {
                    // ignore, fall through
                }
            }
        }

        return new JsonObject();
    }

    private static string? GetString(JsonObject obj, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (obj.TryGetPropertyValue(name, out var node) && node is JsonValue value)
            {
                if (value.TryGetValue(out string? result) && !string.IsNullOrWhiteSpace(result))
                {
                    return result;
                }
            }
        }

        return null;
    }
}
