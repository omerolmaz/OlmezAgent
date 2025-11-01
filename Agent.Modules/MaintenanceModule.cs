using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Agent.Modules;

public sealed class MaintenanceModule : AgentModuleBase
{
    private static readonly IReadOnlyCollection<string> Actions = new[]
    {
        "agentupdate",
        "agentupdateex",
        "downloadfile",
        "reinstall",
        "log",
        "versions"
    };

    private static readonly HttpClient HttpClient = new();

    public MaintenanceModule(ILogger<MaintenanceModule> logger)
        : base(logger)
    {
    }

    public override string Name => "MaintenanceModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        switch (command.Action.ToLowerInvariant())
        {
            case "log":
                await HandleLogAsync(command, context).ConfigureAwait(false);
                return true;
            case "versions":
                await HandleVersionsAsync(command, context).ConfigureAwait(false);
                return true;
            case "agentupdate":
            case "agentupdateex":
                await HandleAgentUpdateAsync(command, context).ConfigureAwait(false);
                return true;
            case "downloadfile":
                await HandleDownloadFileAsync(command, context).ConfigureAwait(false);
                return true;
            case "reinstall":
                await HandleReinstallAsync(command, context).ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleLogAsync(AgentCommand command, AgentContext context)
    {
        var lines = new List<string>();
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "agent.log");
            if (File.Exists(logPath))
            {
                var allLines = await File.ReadAllLinesAsync(logPath, command.CancellationToken).ConfigureAwait(false);
                var take = Math.Min(200, allLines.Length);
                lines.AddRange(allLines[^take..]);
            }
        }
        catch (Exception ex)
        {
            lines.Add($"Log read error: {ex.Message}");
        }

        var logArray = new JsonArray();
        foreach (var line in lines)
        {
            logArray.Add(line);
        }

        var payload = new JsonObject
        {
            ["entries"] = logArray
        };

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            payload)).ConfigureAwait(false);
    }

    private async Task HandleVersionsAsync(AgentCommand command, AgentContext context)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0";
        var payload = new JsonObject
        {
            ["agentVersion"] = version,
            ["framework"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ["os"] = Environment.OSVersion.VersionString,
            ["processArchitecture"] = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            ["machineName"] = Environment.MachineName
        };

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            payload)).ConfigureAwait(false);
    }

    private async Task HandleAgentUpdateAsync(AgentCommand command, AgentContext context)
    {
        if (!command.Payload.TryGetProperty("url", out var urlElement) || urlElement.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            await SendNotImplementedAsync(command, context, "agentupdate requires 'url'.").ConfigureAwait(false);
            return;
        }

        var url = urlElement.GetString()!;
        var expectedHash = command.Payload.TryGetProperty("hash", out var hashElement) && hashElement.ValueKind == System.Text.Json.JsonValueKind.String
            ? hashElement.GetString()
            : null;

        var payload = new JsonObject { ["url"] = url };

        try
        {
            Logger.LogInformation("Agent update başlatılıyor: {Url}", url);

            // Dosyayı indir
            var tempFile = Path.Combine(Path.GetTempPath(), $"AgentUpdate_{Guid.NewGuid():N}.exe");
            await DownloadFileWithHashAsync(url, tempFile, expectedHash).ConfigureAwait(false);

            payload["downloaded"] = true;
            payload["tempFile"] = tempFile;

            // Mevcut executable'ın yolunu al
            var currentExe = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
            var backupExe = currentExe + ".backup";

            // Backup oluştur
            if (File.Exists(currentExe))
            {
                File.Copy(currentExe, backupExe, overwrite: true);
                payload["backupCreated"] = true;
            }

            // Güncelleme scriptini oluştur (batch dosyası)
            var updateScript = Path.Combine(Path.GetTempPath(), "AgentUpdate.bat");
            var scriptContent = $@"@echo off
timeout /t 2 /nobreak >nul
copy /y ""{tempFile}"" ""{currentExe}""
if %errorlevel% neq 0 (
    echo Update failed, restoring backup
    copy /y ""{backupExe}"" ""{currentExe}""
    exit /b 1
)
del ""{backupExe}""
del ""{tempFile}""
net stop AgentHost
net start AgentHost
del ""%~f0""
";
            await File.WriteAllTextAsync(updateScript, scriptContent).ConfigureAwait(false);

            // Script'i başlat ve agent'ı kapat
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{updateScript}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            payload["updateScheduled"] = true;
            payload["willRestart"] = true;

            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.NodeId,
                command.SessionId,
                payload,
                Success: true)).ConfigureAwait(false);

            // Agent'ı kapat
            Logger.LogWarning("Agent güncelleniyor ve yeniden başlatılıyor...");
            await Task.Delay(1000).ConfigureAwait(false); // Response gönderilsin
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Agent update hatası");
            payload["error"] = ex.Message;
            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.NodeId,
                command.SessionId,
                payload,
                Success: false,
                Error: ex.Message)).ConfigureAwait(false);
        }
    }

    private async Task HandleDownloadFileAsync(AgentCommand command, AgentContext context)
    {
        if (!command.Payload.TryGetProperty("url", out var urlElement) || urlElement.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            await SendNotImplementedAsync(command, context, "downloadfile requires 'url'.").ConfigureAwait(false);
            return;
        }

        var url = urlElement.GetString()!;
        var targetPath = command.Payload.TryGetProperty("target", out var targetElement) && targetElement.ValueKind == System.Text.Json.JsonValueKind.String
            ? targetElement.GetString()!
            : Path.Combine(Path.GetTempPath(), Path.GetFileName(new Uri(url).LocalPath));
        var expectedHash = command.Payload.TryGetProperty("hash", out var hashElement) && hashElement.ValueKind == System.Text.Json.JsonValueKind.String
            ? hashElement.GetString()
            : null;

        var payload = new JsonObject { ["url"] = url, ["target"] = targetPath };

        try
        {
            await DownloadFileWithHashAsync(url, targetPath, expectedHash).ConfigureAwait(false);

            var fileInfo = new FileInfo(targetPath);
            payload["downloaded"] = true;
            payload["size"] = fileInfo.Length;
            payload["hash"] = ComputeSHA384(targetPath);

            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.NodeId,
                command.SessionId,
                payload,
                Success: true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Dosya indirme hatası: {Url}", url);
            payload["error"] = ex.Message;
            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.NodeId,
                command.SessionId,
                payload,
                Success: false,
                Error: ex.Message)).ConfigureAwait(false);
        }
    }

    private async Task HandleReinstallAsync(AgentCommand command, AgentContext context)
    {
        var payload = new JsonObject();

        try
        {
            // Agent'ı yeniden yükle (service'i durdur, dosyaları sil, tekrar yükle)
            Logger.LogWarning("Agent reinstall başlatılıyor...");

            var currentExe = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
            var installDir = Path.GetDirectoryName(currentExe)!;

            // Reinstall script oluştur
            var reinstallScript = Path.Combine(Path.GetTempPath(), "AgentReinstall.bat");
            var scriptContent = $@"@echo off
echo Stopping agent service...
net stop AgentHost
timeout /t 2 /nobreak >nul

echo Cleaning installation directory...
del /q ""{installDir}\*.*""

echo Agent uninstalled. Please reinstall manually.
del ""%~f0""
";
            await File.WriteAllTextAsync(reinstallScript, scriptContent).ConfigureAwait(false);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{reinstallScript}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            payload["reinstallScheduled"] = true;

            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.NodeId,
                command.SessionId,
                payload,
                Success: true)).ConfigureAwait(false);

            await Task.Delay(1000).ConfigureAwait(false);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Reinstall hatası");
            payload["error"] = ex.Message;
            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.NodeId,
                command.SessionId,
                payload,
                Success: false,
                Error: ex.Message)).ConfigureAwait(false);
        }
    }

    private async Task DownloadFileWithHashAsync(string url, string targetPath, string? expectedHash)
    {
        Logger.LogInformation("Dosya indiriliyor: {Url} -> {Target}", url, targetPath);

        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream).ConfigureAwait(false);

        Logger.LogInformation("Dosya indirildi: {Size} bytes", fileStream.Length);

        // Hash doğrulama
        if (!string.IsNullOrWhiteSpace(expectedHash))
        {
            var actualHash = ComputeSHA384(targetPath);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(targetPath);
                throw new SecurityException($"Hash mismatch! Expected: {expectedHash}, Actual: {actualHash}");
            }

            Logger.LogInformation("Hash doğrulandı: {Hash}", actualHash);
        }
    }

    private string ComputeSHA384(string filePath)
    {
        using var sha = SHA384.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}
