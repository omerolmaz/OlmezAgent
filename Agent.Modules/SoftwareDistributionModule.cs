using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Agent.Modules;

public sealed class SoftwareDistributionModule : AgentModuleBase
{
    private static readonly IReadOnlyCollection<string> Actions = new[]
    {
        "installsoftware",
        "uninstallsoftware",
        "installupdates",
        "schedulepatch"
    };

    public SoftwareDistributionModule(ILogger<SoftwareDistributionModule> logger)
        : base(logger)
    {
    }

    public override string Name => "SoftwareDistributionModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        if (!OperatingSystem.IsWindows())
        {
            await SendNotImplementedAsync(command, context, "Software distribution is only supported on Windows.")
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
            case "installupdates":
                await HandleInstallUpdatesAsync(command, context).ConfigureAwait(false);
                return true;
            case "schedulepatch":
                await HandleSchedulePatchAsync(command, context).ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleInstallSoftwareAsync(AgentCommand command, AgentContext context)
    {
        if (!command.Payload.TryGetProperty("path", out var pathElement) || pathElement.ValueKind != JsonValueKind.String)
        {
            await SendNotImplementedAsync(command, context, "installsoftware requires 'path'.").ConfigureAwait(false);
            return;
        }

        var installerPath = pathElement.GetString()!;
        var silent = !command.Payload.TryGetProperty("silent", out var silentElement) || silentElement.ValueKind != JsonValueKind.False;
        var additionalArgs = command.Payload.TryGetProperty("arguments", out var argsElement) && argsElement.ValueKind == JsonValueKind.String
            ? argsElement.GetString()
            : null;
        var workingDirectory = command.Payload.TryGetProperty("workingDirectory", out var wdElement) && wdElement.ValueKind == JsonValueKind.String
            ? wdElement.GetString()
            : Path.GetDirectoryName(installerPath);

        if (!File.Exists(installerPath) && !installerPath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
        {
            await SendNotImplementedAsync(command, context, $"Installer not found: {installerPath}").ConfigureAwait(false);
            return;
        }

        var startInfo = BuildInstallProcessStartInfo(installerPath, silent, additionalArgs, workingDirectory);
        var result = await ExecuteProcessAsync(startInfo).ConfigureAwait(false);

        var payload = new JsonObject
        {
            ["path"] = installerPath,
            ["exitCode"] = result.ExitCode,
            ["output"] = result.StandardOutput,
            ["errors"] = result.StandardError
        };

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: result.ExitCode == 0,
            Error: result.ExitCode == 0 ? null : "Installer exited with non-zero code")).ConfigureAwait(false);
    }

    private async Task HandleUninstallSoftwareAsync(AgentCommand command, AgentContext context)
    {
        if (!command.Payload.TryGetProperty("softwareName", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
        {
            await SendNotImplementedAsync(command, context, "uninstallsoftware requires 'softwareName'.").ConfigureAwait(false);
            return;
        }

        var softwareName = nameElement.GetString()!;
        var escapedName = softwareName.Replace("'", "''", StringComparison.Ordinal);
        var script = "$app = Get-WmiObject -Class Win32_Product | Where-Object { $_.Name -like '*" + escapedName + "*' } | Select-Object -First 1; " +
                     "if($app) { $result = $app.Uninstall(); if($result.ReturnValue -eq 0) { Write-Output 'Success' } else { Write-Output ('Error:' + $result.ReturnValue) } } " +
                     "else { Write-Output 'NotFound' }";

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -NonInteractive -Command \"" + script.Replace("\"", "\\\"") + "\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var result = await ExecuteProcessAsync(startInfo).ConfigureAwait(false);
        var payload = new JsonObject
        {
            ["softwareName"] = softwareName,
            ["exitCode"] = result.ExitCode,
            ["output"] = result.StandardOutput,
            ["errors"] = result.StandardError
        };

        var success = result.ExitCode == 0 && result.StandardOutput.Contains("Success", StringComparison.OrdinalIgnoreCase);
        var error = success
            ? null
            : (result.StandardOutput.Contains("NotFound", StringComparison.OrdinalIgnoreCase) ? "Software not found." : result.StandardError);

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: success,
            Error: error)).ConfigureAwait(false);
    }

    private async Task HandleInstallUpdatesAsync(AgentCommand command, AgentContext context)
    {
        var payload = new JsonObject();
        try
        {
            var updateSessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
            if (updateSessionType == null)
            {
                throw new InvalidOperationException("Microsoft.Update.Session COM object not available.");
            }

            dynamic session = Activator.CreateInstance(updateSessionType)!;
            dynamic searcher = session.CreateUpdateSearcher();
            dynamic searchResult = searcher.Search("IsInstalled=0 and Type='Software'");
            dynamic updates = searchResult.Updates;

            if (updates.Count == 0)
            {
                payload["message"] = "No updates available.";
                await context.ResponseWriter.SendAsync(new CommandResult(command.Action, command.CommandId, command.NodeId, command.SessionId, payload))
                    .ConfigureAwait(false);
                return;
            }

            dynamic downloader = session.CreateUpdateDownloader();
            downloader.Updates = updates;
            var downloadResult = downloader.Download();

            dynamic installer = session.CreateUpdateInstaller();
            installer.Updates = updates;
            var installResult = installer.Install();

            payload["downloadResult"] = JsonSerializer.SerializeToNode(new
            {
                ResultCode = downloadResult.ResultCode,
                HResult = downloadResult.HResult
            });
            payload["installResult"] = JsonSerializer.SerializeToNode(new
            {
                ResultCode = installResult.ResultCode,
                HResult = installResult.HResult,
                RebootRequired = installResult.RebootRequired
            });

            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.CommandId,
                command.NodeId,
                command.SessionId,
                payload,
                Success: true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.CommandId,
                command.NodeId,
                command.SessionId,
                payload,
                Success: false,
                Error: ex.Message)).ConfigureAwait(false);
        }
    }

    private async Task HandleSchedulePatchAsync(AgentCommand command, AgentContext context)
    {
        if (!command.Payload.TryGetProperty("scheduledTime", out var timeElement))
        {
            await SendNotImplementedAsync(command, context, "schedulepatch requires 'scheduledTime'.").ConfigureAwait(false);
            return;
        }

        DateTimeOffset scheduled;
        if (timeElement.ValueKind == JsonValueKind.String)
        {
            if (!DateTimeOffset.TryParse(timeElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out scheduled))
            {
                await SendNotImplementedAsync(command, context, "Invalid scheduledTime format.").ConfigureAwait(false);
                return;
            }
        }
        else if (timeElement.ValueKind == JsonValueKind.Number && timeElement.TryGetInt64(out var unixMs))
        {
            scheduled = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).ToLocalTime();
        }
        else
        {
            await SendNotImplementedAsync(command, context, "schedulepatch requires a string or epoch milliseconds.")
                .ConfigureAwait(false);
            return;
        }

        if (scheduled < DateTimeOffset.Now.AddMinutes(1))
        {
            await SendNotImplementedAsync(command, context, "scheduledTime must be at least 1 minute in the future.")
                .ConfigureAwait(false);
            return;
        }

        var taskName = command.Payload.TryGetProperty("taskName", out var taskElement) && taskElement.ValueKind == JsonValueKind.String
            ? taskElement.GetString()!
            : $"AgentPatch_{Guid.NewGuid():N}";

        var powershellCommand =
            "$session = New-Object -ComObject Microsoft.Update.Session; " +
            "$searcher = $session.CreateUpdateSearcher(); " +
            "$result = $searcher.Search(\"IsInstalled=0 and Type='Software'\"); " +
            "if($result.Updates.Count -gt 0) { " +
            "$downloader = $session.CreateUpdateDownloader(); $downloader.Updates = $result.Updates; $downloader.Download(); " +
            "$installer = $session.CreateUpdateInstaller(); $installer.Updates = $result.Updates; $installer.Install(); }";

        var escapedPs = powershellCommand.Replace("\"", "`\"", StringComparison.Ordinal);
        var taskRunCommand = $"powershell.exe -NoProfile -WindowStyle Hidden -Command \"{escapedPs}\"";

        var arguments = string.Format(CultureInfo.InvariantCulture,
            "/Create /TN \"{0}\" /SC ONCE /ST {1:HH\\:mm} /SD {1:MM/dd/yyyy} /TR \"{2}\" /RL HIGHEST /F",
            taskName,
            scheduled.ToLocalTime(),
            taskRunCommand.Replace("\"", "\\\"", StringComparison.Ordinal));

        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var result = await ExecuteProcessAsync(startInfo).ConfigureAwait(false);
        var payload = new JsonObject
        {
            ["taskName"] = taskName,
            ["scheduledTime"] = scheduled.ToString("O"),
            ["exitCode"] = result.ExitCode,
            ["output"] = result.StandardOutput,
            ["errors"] = result.StandardError
        };

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payload,
            Success: result.ExitCode == 0,
            Error: result.ExitCode == 0 ? null : result.StandardError)).ConfigureAwait(false);
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
}
