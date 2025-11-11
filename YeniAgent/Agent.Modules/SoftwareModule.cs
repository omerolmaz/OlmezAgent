using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Agent.Modules;

public sealed class SoftwareModule : AgentModuleBase
{
    private static readonly IReadOnlyCollection<string> Actions = new[]
    {
        "installsoftware",
        "uninstallsoftware",
        "installwithchoco",
        "installchoco"
    };

    private static readonly HttpClient HttpClient = new();

    public SoftwareModule(ILogger<SoftwareModule> logger) : base(logger)
    {
    }

    public override string Name => "SoftwareModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        Logger.LogInformation("SoftwareModule.HandleAsync called: {Action}", command.Action);

        if (!OperatingSystem.IsWindows())
        {
            await SendNotImplementedAsync(command, context, "Software management is currently supported on Windows only.")
                .ConfigureAwait(false);
            return true;
        }

        switch (command.Action.ToLowerInvariant())
        {
            case "installsoftware":
                await HandleInstallSoftwareAsync(command, context).ConfigureAwait(false);
                return true;
            case "uninstallsoftware":
                await HandleUninstallSoftwareAsync(command, context).ConfigureAwait(false);
                return true;
            case "installwithchoco":
                await HandleInstallWithChocoAsync(command, context).ConfigureAwait(false);
                return true;
            case "installchoco":
                await HandleInstallChocoAsync(command, context).ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleInstallSoftwareAsync(AgentCommand command, AgentContext context)
    {
        try
        {
            string? filePath = null;
            string? downloadUrl = null;
            string? fileName = null;
            string arguments = string.Empty;
            int timeout = 1800;

            if (command.Payload.TryGetProperty("filePath", out var filePathElem))
                filePath = filePathElem.GetString();
            if (command.Payload.TryGetProperty("downloadUrl", out var downloadUrlElem))
                downloadUrl = downloadUrlElem.GetString();
            if (command.Payload.TryGetProperty("fileName", out var fileNameElem))
                fileName = fileNameElem.GetString();
            if (command.Payload.TryGetProperty("arguments", out var argsElem))
                arguments = argsElem.GetString() ?? string.Empty;
            if (command.Payload.TryGetProperty("timeout", out var timeoutElem))
                timeout = timeoutElem.GetInt32();

            // URL'den indirme varsa önce dosyayı indir
            if (!string.IsNullOrWhiteSpace(downloadUrl))
            {
                Logger.LogInformation("Downloading software from: {DownloadUrl}", downloadUrl);
                
                var tempDir = Path.Combine(Path.GetTempPath(), "OlmezAgent");
                Directory.CreateDirectory(tempDir);
                
                fileName = fileName ?? Path.GetFileName(new Uri(downloadUrl).LocalPath);
                filePath = Path.Combine(tempDir, fileName);

                try
                {
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromMinutes(10); // 10 dakika timeout

                    Logger.LogInformation("Starting download to: {FilePath}", filePath);
                    
                    var response = await httpClient.GetAsync(downloadUrl).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    Logger.LogInformation("Download size: {Size} bytes", totalBytes);

                    await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await using var downloadStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    
                    await downloadStream.CopyToAsync(fileStream).ConfigureAwait(false);
                    
                    Logger.LogInformation("Download completed: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to download file from {DownloadUrl}", downloadUrl);
                    var errorPayload = new JsonObject { 
                        ["error"] = $"Download failed: {ex.Message}",
                        ["downloadUrl"] = downloadUrl
                    };
                    await context.ResponseWriter.SendAsync(new CommandResult(
                        command.Action, command.CommandId, command.NodeId, command.SessionId,
                        errorPayload, Success: false, Error: "DownloadFailed"))
                        .ConfigureAwait(false);
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                var errorPayload = new JsonObject { ["error"] = "File path or download URL is required" };
                await context.ResponseWriter.SendAsync(new CommandResult(
                    command.Action, command.CommandId, command.NodeId, command.SessionId,
                    errorPayload, Success: false, Error: "ValidationError"))
                    .ConfigureAwait(false);
                return;
            }

            if (!File.Exists(filePath))
            {
                var errorPayload = new JsonObject { ["error"] = $"File not found: {filePath}" };
                await context.ResponseWriter.SendAsync(new CommandResult(
                    command.Action, command.CommandId, command.NodeId, command.SessionId,
                    errorPayload, Success: false, Error: "FileNotFound"))
                    .ConfigureAwait(false);
                return;
            }

            Logger.LogInformation("Installing software from: {FilePath}", filePath);

            // TacticalRMM gibi: Kullanıcının gönderdiği parametreleri olduğu gibi kullan
            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                Arguments = arguments,  // Backend'den gelen parametreleri olduğu gibi kullan
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Logger.LogInformation("Starting install process: {FileName} {Arguments}", filePath, arguments);

            string output = "";
            string error = "";
            int exitCode;

            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                Logger.LogInformation("Install process started with PID: {ProcessId}", process.Id);

                // Output ve error'u asenkron oku
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                var completed = await Task.Run(() => process.WaitForExit(timeout * 1000)).ConfigureAwait(false);

                if (!completed)
                {
                    Logger.LogWarning("Installation timeout after {Timeout}s, killing process tree", timeout);
                    KillProcessTree(process.Id);
                    
                    var timeoutPayload = new JsonObject
                    {
                        ["output"] = "",
                        ["error"] = "Installation timed out",
                        ["exitCode"] = -1,
                        ["timedOut"] = true
                    };
                    await context.ResponseWriter.SendAsync(new CommandResult(
                        command.Action, command.CommandId, command.NodeId, command.SessionId,
                        timeoutPayload, Success: false, Error: "Timeout"))
                        .ConfigureAwait(false);
                    return;
                }

                // Output ve error'u al
                output = await outputTask;
                error = await errorTask;

                exitCode = process.ExitCode;
                Logger.LogInformation("Install process completed with exit code: {ExitCode}", exitCode);
            }

            var successPayload = new JsonObject
            {
                ["exitCode"] = exitCode,
                ["output"] = output.ToString(),
                ["error"] = error.ToString(),
                ["timedOut"] = false,
                ["refreshInventory"] = exitCode == 0  // Frontend'e inventory yenilemesini söyle
            };

            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action, command.CommandId, command.NodeId, command.SessionId,
                successPayload, Success: exitCode == 0, Error: exitCode != 0 ? "InstallFailed" : null))
                .ConfigureAwait(false);

            // URL'den indirilen dosyayı temizle
            if (!string.IsNullOrWhiteSpace(downloadUrl) && File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    Logger.LogInformation("Cleaned up downloaded file: {FilePath}", filePath);
                }
                catch (Exception cleanupEx)
                {
                    Logger.LogWarning(cleanupEx, "Failed to delete downloaded file: {FilePath}", filePath);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error installing software");
            var errorPayload = new JsonObject { ["error"] = ex.Message };
            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action, command.CommandId, command.NodeId, command.SessionId,
                errorPayload, Success: false, Error: "Exception"))
                .ConfigureAwait(false);
        }
    }

    private async Task HandleUninstallSoftwareAsync(AgentCommand command, AgentContext context)
    {
        try
        {
            string? softwareName = null;
            string? uninstallString = null;
            int timeout = 1800;

            if (command.Payload.TryGetProperty("softwareName", out var nameElem))
                softwareName = nameElem.GetString();
            if (command.Payload.TryGetProperty("uninstallString", out var uninstallElem))
                uninstallString = uninstallElem.GetString();
            if (command.Payload.TryGetProperty("timeout", out var timeoutElem))
                timeout = timeoutElem.GetInt32();

            if (string.IsNullOrWhiteSpace(uninstallString))
            {
                var errorPayload = new JsonObject { ["error"] = "Uninstall string is required" };
                await context.ResponseWriter.SendAsync(new CommandResult(
                    command.Action, command.CommandId, command.NodeId, command.SessionId,
                    errorPayload, Success: false, Error: "ValidationError"))
                    .ConfigureAwait(false);
                return;
            }

            if (!string.IsNullOrWhiteSpace(softwareName) && 
                softwareName.Contains("olmezagent", StringComparison.OrdinalIgnoreCase))
            {
                var errorPayload = new JsonObject { ["error"] = "Cannot uninstall the agent itself" };
                await context.ResponseWriter.SendAsync(new CommandResult(
                    command.Action, command.CommandId, command.NodeId, command.SessionId,
                    errorPayload, Success: false, Error: "Forbidden"))
                    .ConfigureAwait(false);
                return;
            }

            Logger.LogInformation("Uninstalling software: {Name}", softwareName);

            var parts = ParseCommandLine(uninstallString);
            if (parts.Length == 0)
            {
                var errorPayload = new JsonObject { ["error"] = "Invalid uninstall command" };
                await context.ResponseWriter.SendAsync(new CommandResult(
                    command.Action, command.CommandId, command.NodeId, command.SessionId,
                    errorPayload, Success: false, Error: "ValidationError"))
                    .ConfigureAwait(false);
                return;
            }

            var fileName = parts[0];
            var arguments = string.Join(" ", parts.Skip(1));

            // TacticalRMM gibi: UninstallString'i olduğu gibi kullan, parametre ekleme!
            
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,           // Output yakalayabilmek için
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,     // Output'u yakala
                RedirectStandardError = true       // Error'u yakala
            };

            Logger.LogInformation("Starting uninstall process: {FileName} {Arguments}", fileName, arguments);

            int exitCode;
            string output = "";
            string error = "";

            using (var process = new Process { StartInfo = psi })
            {
                Logger.LogInformation("Process.Start() called...");
                process.Start();
                Logger.LogInformation("Process started with PID: {ProcessId}", process.Id);

                Logger.LogInformation("Waiting for process to complete (timeout: {Timeout}s)...", timeout);
                
                // Output ve error'u asenkron oku
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                
                var completed = await Task.Run(() => process.WaitForExit(timeout * 1000)).ConfigureAwait(false);

                if (!completed)
                {
                    Logger.LogWarning("Uninstall timeout after {Timeout}s", timeout);
                    KillProcessTree(process.Id);
                    
                    var timeoutPayload = new JsonObject
                    {
                        ["output"] = "",
                        ["error"] = "Uninstall timed out",
                        ["exitCode"] = -1,
                        ["timedOut"] = true
                    };
                    await context.ResponseWriter.SendAsync(new CommandResult(
                        command.Action, command.CommandId, command.NodeId, command.SessionId,
                        timeoutPayload, Success: false, Error: "Timeout"))
                        .ConfigureAwait(false);
                    return;
                }

                // Output ve error'u al
                output = await outputTask;
                error = await errorTask;
                
                exitCode = process.ExitCode;
                Logger.LogInformation("Process completed with exit code: {ExitCode}", exitCode);
            }

            Logger.LogInformation("Uninstall process finished. ExitCode: {ExitCode}, Success: {Success}", exitCode, exitCode == 0);

            var successPayload = new JsonObject
            {
                ["exitCode"] = exitCode,
                ["output"] = output,
                ["error"] = error,
                ["timedOut"] = false,
                ["refreshInventory"] = exitCode == 0  // Frontend'e inventory yenilemesini söyle
            };

            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action, command.CommandId, command.NodeId, command.SessionId,
                successPayload, Success: exitCode == 0, Error: exitCode != 0 ? "UninstallFailed" : null))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uninstalling software");
            var errorPayload = new JsonObject { ["error"] = ex.Message };
            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action, command.CommandId, command.NodeId, command.SessionId,
                errorPayload, Success: false, Error: "Exception"))
                .ConfigureAwait(false);
        }
    }

    private async Task HandleInstallChocoAsync(AgentCommand command, AgentContext context)
    {
        try
        {
            var chocoPath = FindChocolateyExe();
            if (!string.IsNullOrEmpty(chocoPath))
            {
                var payload = new JsonObject
                {
                    ["message"] = "Chocolatey is already installed",
                    ["path"] = chocoPath
                };
                await context.ResponseWriter.SendAsync(new CommandResult(
                    command.Action, command.CommandId, command.NodeId, command.SessionId,
                    payload, Success: true))
                    .ConfigureAwait(false);
                return;
            }

            Logger.LogInformation("Installing Chocolatey...");

            HttpClient.Timeout = TimeSpan.FromMinutes(5);
            var script = await HttpClient.GetStringAsync("https://chocolatey.org/install.ps1").ConfigureAwait(false);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command -",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            string output;
            string error;
            int exitCode;

            using (var process = new Process { StartInfo = psi })
            {
                process.Start();

                await process.StandardInput.WriteAsync(script).ConfigureAwait(false);
                await process.StandardInput.FlushAsync().ConfigureAwait(false);
                process.StandardInput.Close();

                // Output ve error'u asenkron oku
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                var completed = await Task.Run(() => process.WaitForExit(300000)).ConfigureAwait(false);

                if (!completed)
                {
                    Logger.LogWarning("Chocolatey install timeout");
                    KillProcessTree(process.Id);
                    
                    var timeoutPayload = new JsonObject
                    {
                        ["output"] = "",
                        ["error"] = "Chocolatey installation timed out",
                        ["exitCode"] = -1,
                        ["timedOut"] = true
                    };
                    await context.ResponseWriter.SendAsync(new CommandResult(
                        command.Action, command.CommandId, command.NodeId, command.SessionId,
                        timeoutPayload, Success: false, Error: "Timeout"))
                        .ConfigureAwait(false);
                    return;
                }

                // Output ve error'u al
                output = await outputTask;
                error = await errorTask;

                exitCode = process.ExitCode;
                Logger.LogInformation("Install process completed with exit code: {ExitCode}", exitCode);
            }

            var successPayload = new JsonObject
            {
                ["exitCode"] = exitCode,
                ["output"] = output,
                ["error"] = error,
                ["timedOut"] = false
            };

            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action, command.CommandId, command.NodeId, command.SessionId,
                successPayload, Success: exitCode == 0, Error: exitCode != 0 ? "InstallFailed" : null))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error installing Chocolatey");
            var errorPayload = new JsonObject { ["error"] = ex.Message };
            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action, command.CommandId, command.NodeId, command.SessionId,
                errorPayload, Success: false, Error: "Exception"))
                .ConfigureAwait(false);
        }
    }

    private async Task HandleInstallWithChocoAsync(AgentCommand command, AgentContext context)
    {
        try
        {
            string? packageName = null;
            string? version = null;
            bool force = false;

            if (command.Payload.TryGetProperty("packageName", out var pkgElem))
                packageName = pkgElem.GetString();
            if (command.Payload.TryGetProperty("version", out var verElem))
                version = verElem.GetString();
            if (command.Payload.TryGetProperty("force", out var forceElem))
                force = forceElem.GetBoolean();

            if (string.IsNullOrWhiteSpace(packageName))
            {
                var errorPayload = new JsonObject { ["error"] = "Package name is required" };
                await context.ResponseWriter.SendAsync(new CommandResult(
                    command.Action, command.CommandId, command.NodeId, command.SessionId,
                    errorPayload, Success: false, Error: "ValidationError"))
                    .ConfigureAwait(false);
                return;
            }

            var chocoPath = FindChocolateyExe();
            if (string.IsNullOrEmpty(chocoPath))
            {
                var errorPayload = new JsonObject { ["error"] = "Chocolatey is not installed. Please install Chocolatey first." };
                await context.ResponseWriter.SendAsync(new CommandResult(
                    command.Action, command.CommandId, command.NodeId, command.SessionId,
                    errorPayload, Success: false, Error: "ChocoNotInstalled"))
                    .ConfigureAwait(false);
                return;
            }

            Logger.LogInformation("Installing package with Chocolatey: {Package}", packageName);

            var args = $"install {packageName} --yes --force --force-dependencies --no-progress";
            if (!string.IsNullOrWhiteSpace(version))
                args += $" --version={version}";

            var psi = new ProcessStartInfo
            {
                FileName = chocoPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            string output;
            string error;
            int exitCode;

            using (var process = new Process { StartInfo = psi })
            {
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                var completed = await Task.Run(() => process.WaitForExit(1200000)).ConfigureAwait(false);

                if (!completed)
                {
                    Logger.LogWarning("Chocolatey install timeout for {Package}", packageName);
                    KillProcessTree(process.Id);

                    output = await outputTask.ConfigureAwait(false);
                    
                    var timeoutPayload = new JsonObject
                    {
                        ["output"] = output,
                        ["error"] = "Package installation timed out",
                        ["exitCode"] = -1,
                        ["timedOut"] = true
                    };
                    await context.ResponseWriter.SendAsync(new CommandResult(
                        command.Action, command.CommandId, command.NodeId, command.SessionId,
                        timeoutPayload, Success: false, Error: "Timeout"))
                        .ConfigureAwait(false);
                    return;
                }

                output = await outputTask.ConfigureAwait(false);
                error = await errorTask.ConfigureAwait(false);
                exitCode = process.ExitCode;
            }

            var resultPayload = new JsonObject
            {
                ["exitCode"] = exitCode,
                ["output"] = output,
                ["error"] = error,
                ["timedOut"] = false
            };

            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action, command.CommandId, command.NodeId, command.SessionId,
                resultPayload, Success: exitCode == 0, Error: exitCode != 0 ? "InstallFailed" : null))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error installing package with Chocolatey");
            var errorPayload = new JsonObject { ["error"] = ex.Message };
            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action, command.CommandId, command.NodeId, command.SessionId,
                errorPayload, Success: false, Error: "Exception"))
                .ConfigureAwait(false);
        }
    }

    private static string? FindChocolateyExe()
    {
        var paths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "bin", "choco.exe"),
            Path.Combine(Environment.GetEnvironmentVariable("ProgramData") ?? "C:\\ProgramData", "chocolatey", "bin", "choco.exe")
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
                return path;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "choco",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    var firstLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(firstLine) && File.Exists(firstLine))
                        return firstLine;
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private static string[] ParseCommandLine(string commandLine)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < commandLine.Length; i++)
        {
            var c = commandLine[i];
            if (c == '"' && (i == 0 || commandLine[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            parts.Add(current.ToString());

        return parts.ToArray();
    }

    private static void KillProcessTree(int pid)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/PID {pid} /T /F",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(5000);
        }
        catch
        {
            // Ignore errors
        }
    }
}
