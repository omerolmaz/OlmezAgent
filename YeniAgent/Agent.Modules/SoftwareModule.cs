using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Modules;

/// <summary>
/// Software management module for installing and uninstalling applications
/// </summary>
public sealed class SoftwareModule : AgentModuleBase
{
    private static readonly IReadOnlyCollection<string> Actions = new[]
    {
        "installsoftware",
        "uninstallsoftware",
        "installwithchoco",
        "installchoco"
    };

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

    /// <summary>
    /// Install software from local file (MSI, EXE, etc.)
    /// </summary>
    private async Task HandleInstallSoftwareAsync(AgentCommand command, AgentContext context)
    {
        try
        {
            var filePath = command.Data["filePath"]?.GetValue<string>();
            var arguments = command.Data["arguments"]?.GetValue<string>() ?? "";
            var timeout = command.Data["timeout"]?.GetValue<int>() ?? 1800; // 30 minutes default
            var runAsUser = command.Data["runAsUser"]?.GetValue<bool>() ?? false;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                await SendErrorAsync(command, context, "File path is required").ConfigureAwait(false);
                return;
            }

            if (!File.Exists(filePath))
            {
                await SendErrorAsync(command, context, $"File not found: {filePath}").ConfigureAwait(false);
                return;
            }

            Logger.LogInformation("Installing software from: {FilePath}", filePath);

            // Determine installer type and default arguments
            var fileExt = Path.GetExtension(filePath).ToLowerInvariant();
            var finalArgs = arguments;

            if (string.IsNullOrWhiteSpace(finalArgs))
            {
                finalArgs = fileExt switch
                {
                    ".msi" => "/i /qn /norestart",
                    ".exe" => "/S /silent",
                    _ => ""
                };
            }

            // Build command
            var installCmd = fileExt == ".msi" 
                ? $"msiexec.exe {finalArgs} \"{filePath}\""
                : $"\"{filePath}\" {finalArgs}";

            // Execute installation
            var result = await ExecuteCommandAsync(installCmd, timeout, runAsUser).ConfigureAwait(false);

            var response = new JsonObject
            {
                ["success"] = result.ExitCode == 0,
                ["exitCode"] = result.ExitCode,
                ["output"] = result.Output,
                ["error"] = result.Error,
                ["filePath"] = filePath
            };

            if (result.ExitCode == 0)
            {
                Logger.LogInformation("Software installed successfully: {FilePath}", filePath);
                await SendSuccessAsync(command, context, response).ConfigureAwait(false);
            }
            else
            {
                Logger.LogWarning("Software installation failed with exit code {ExitCode}: {FilePath}", result.ExitCode, filePath);
                await SendErrorAsync(command, context, $"Installation failed with exit code {result.ExitCode}", response).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error installing software");
            await SendErrorAsync(command, context, $"Installation error: {ex.Message}").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Uninstall software using provided uninstall string
    /// </summary>
    private async Task HandleUninstallSoftwareAsync(AgentCommand command, AgentContext context)
    {
        try
        {
            var name = command.Data["name"]?.GetValue<string>();
            var uninstallCmd = command.Data["command"]?.GetValue<string>();
            var timeout = command.Data["timeout"]?.GetValue<int>() ?? 1800; // 30 minutes default
            var runAsUser = command.Data["runAsUser"]?.GetValue<bool>() ?? false;

            if (string.IsNullOrWhiteSpace(name))
            {
                await SendErrorAsync(command, context, "Software name is required").ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(uninstallCmd))
            {
                await SendErrorAsync(command, context, "Uninstall command is required").ConfigureAwait(false);
                return;
            }

            // Security check: Prevent uninstalling our own agent
            if (uninstallCmd.Contains("olmezagent", StringComparison.OrdinalIgnoreCase) ||
                uninstallCmd.Contains("olmez.exe", StringComparison.OrdinalIgnoreCase))
            {
                await SendErrorAsync(command, context, "Cannot uninstall the Olmez Agent from here").ConfigureAwait(false);
                return;
            }

            Logger.LogInformation("Uninstalling software: {Name}", name);
            Logger.LogInformation("Uninstall command: {Command}", uninstallCmd);

            // Execute uninstall command
            var result = await ExecuteCommandAsync(uninstallCmd, timeout, runAsUser).ConfigureAwait(false);

            var response = new JsonObject
            {
                ["success"] = result.ExitCode == 0 || result.ExitCode == 3010, // 3010 = reboot required
                ["exitCode"] = result.ExitCode,
                ["output"] = result.Output,
                ["error"] = result.Error,
                ["name"] = name,
                ["rebootRequired"] = result.ExitCode == 3010
            };

            if (result.ExitCode == 0 || result.ExitCode == 3010)
            {
                Logger.LogInformation("Software uninstalled successfully: {Name} (Exit code: {ExitCode})", name, result.ExitCode);
                await SendSuccessAsync(command, context, response).ConfigureAwait(false);
            }
            else
            {
                Logger.LogWarning("Software uninstall failed with exit code {ExitCode}: {Name}", result.ExitCode, name);
                await SendErrorAsync(command, context, $"Uninstall failed with exit code {result.ExitCode}", response).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uninstalling software");
            await SendErrorAsync(command, context, $"Uninstall error: {ex.Message}").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Install Chocolatey package manager
    /// </summary>
    private async Task HandleInstallChocoAsync(AgentCommand command, AgentContext context)
    {
        try
        {
            Logger.LogInformation("Installing Chocolatey...");

            // Check if already installed
            if (IsChocolateyInstalled())
            {
                await SendSuccessAsync(command, context, new JsonObject
                {
                    ["installed"] = true,
                    ["message"] = "Chocolatey is already installed"
                }).ConfigureAwait(false);
                return;
            }

            // Download install script
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);
            
            var installScript = await httpClient.GetStringAsync("https://chocolatey.org/install.ps1").ConfigureAwait(false);

            // Execute PowerShell script
            var result = await ExecutePowerShellScriptAsync(installScript, timeout: 900).ConfigureAwait(false);

            var response = new JsonObject
            {
                ["installed"] = result.ExitCode == 0,
                ["exitCode"] = result.ExitCode,
                ["output"] = result.Output,
                ["error"] = result.Error
            };

            if (result.ExitCode == 0)
            {
                Logger.LogInformation("Chocolatey installed successfully");
                await SendSuccessAsync(command, context, response).ConfigureAwait(false);
            }
            else
            {
                Logger.LogWarning("Chocolatey installation failed with exit code {ExitCode}", result.ExitCode);
                await SendErrorAsync(command, context, $"Chocolatey installation failed with exit code {result.ExitCode}", response).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error installing Chocolatey");
            await SendErrorAsync(command, context, $"Chocolatey installation error: {ex.Message}").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Install software using Chocolatey
    /// </summary>
    private async Task HandleInstallWithChocoAsync(AgentCommand command, AgentContext context)
    {
        try
        {
            var packageName = command.Data["packageName"]?.GetValue<string>();
            var version = command.Data["version"]?.GetValue<string>();
            var force = command.Data["force"]?.GetValue<bool>() ?? true;

            if (string.IsNullOrWhiteSpace(packageName))
            {
                await SendErrorAsync(command, context, "Package name is required").ConfigureAwait(false);
                return;
            }

            // Check if Chocolatey is installed
            if (!IsChocolateyInstalled())
            {
                await SendErrorAsync(command, context, "Chocolatey is not installed. Please install Chocolatey first.").ConfigureAwait(false);
                return;
            }

            Logger.LogInformation("Installing package with Chocolatey: {PackageName}", packageName);

            var chocoExe = FindChocolateyExe();
            if (string.IsNullOrWhiteSpace(chocoExe))
            {
                await SendErrorAsync(command, context, "Chocolatey executable not found").ConfigureAwait(false);
                return;
            }

            // Build arguments
            var args = new List<string> { "install", packageName, "--yes", "--no-progress" };
            
            if (!string.IsNullOrWhiteSpace(version))
            {
                args.Add($"--version={version}");
            }

            if (force)
            {
                args.Add("--force");
                args.Add("--force-dependencies");
            }

            var arguments = string.Join(" ", args);
            var installCmd = $"\"{chocoExe}\" {arguments}";

            // Execute installation (20 minutes timeout for large packages)
            var result = await ExecuteCommandAsync(installCmd, timeout: 1200, runAsUser: false).ConfigureAwait(false);

            var response = new JsonObject
            {
                ["success"] = result.ExitCode == 0,
                ["exitCode"] = result.ExitCode,
                ["output"] = result.Output,
                ["error"] = result.Error,
                ["packageName"] = packageName
            };

            if (result.ExitCode == 0)
            {
                Logger.LogInformation("Package installed successfully with Chocolatey: {PackageName}", packageName);
                await SendSuccessAsync(command, context, response).ConfigureAwait(false);
            }
            else
            {
                Logger.LogWarning("Chocolatey package installation failed with exit code {ExitCode}: {PackageName}", result.ExitCode, packageName);
                await SendErrorAsync(command, context, $"Chocolatey installation failed with exit code {result.ExitCode}", response).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error installing with Chocolatey");
            await SendErrorAsync(command, context, $"Chocolatey installation error: {ex.Message}").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Execute command with timeout and user context
    /// </summary>
    private async Task<CommandResult> ExecuteCommandAsync(string command, int timeoutSeconds, bool runAsUser)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {command}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.SystemDirectory
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        var processTask = Task.Run(() => process.WaitForExit(), cts.Token);

        try
        {
            await processTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch { }

            return new CommandResult
            {
                ExitCode = -1,
                Output = outputBuilder.ToString(),
                Error = $"Command timed out after {timeoutSeconds} seconds",
                TimedOut = true
            };
        }

        return new CommandResult
        {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString(),
            Error = errorBuilder.ToString(),
            TimedOut = false
        };
    }

    /// <summary>
    /// Execute PowerShell script
    /// </summary>
    private async Task<CommandResult> ExecutePowerShellScriptAsync(string script, int timeout)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"olmez_script_{Guid.NewGuid():N}.ps1");
        
        try
        {
            await File.WriteAllTextAsync(tempFile, script).ConfigureAwait(false);

            var command = $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{tempFile}\"";
            return await ExecuteCommandAsync(command, timeout, false).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch { }
        }
    }

    /// <summary>
    /// Check if Chocolatey is installed
    /// </summary>
    private bool IsChocolateyInstalled()
    {
        var chocoExe = FindChocolateyExe();
        return !string.IsNullOrWhiteSpace(chocoExe) && File.Exists(chocoExe);
    }

    /// <summary>
    /// Find Chocolatey executable path
    /// </summary>
    private string? FindChocolateyExe()
    {
        // 1. Check in PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var chocoInPath = pathEnv
            .Split(';')
            .Select(p => Path.Combine(p.Trim(), "choco.exe"))
            .FirstOrDefault(File.Exists);

        if (!string.IsNullOrWhiteSpace(chocoInPath))
            return chocoInPath;

        // 2. Check default location
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var defaultPath = Path.Combine(programData, @"chocolatey\bin\choco.exe");
        
        if (File.Exists(defaultPath))
            return defaultPath;

        // 3. Try to find via where command
        try
        {
            var whereResult = ExecuteCommandAsync("where choco.exe", 5, false).Result;
            if (whereResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(whereResult.Output))
            {
                var path = whereResult.Output.Split('\n')[0].Trim();
                if (File.Exists(path))
                    return path;
            }
        }
        catch { }

        return null;
    }

    private class CommandResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public bool TimedOut { get; set; }
    }
}
