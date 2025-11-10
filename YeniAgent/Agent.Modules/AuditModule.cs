using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Modules;

public sealed class AuditModule : AgentModuleBase
{
    private static readonly IReadOnlyCollection<string> Actions = new[]
    {
        "getauditlogs",
        "clearauditlogs"
    };

    private readonly ConcurrentQueue<AuditEntry> _auditLog = new();
    private readonly string _auditLogPath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private const int MaxInMemoryEntries = 1000;
    private const int MaxFileEntries = 10000;

    public AuditModule(ILogger<AuditModule> logger) : base(logger)
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);
        _auditLogPath = Path.Combine(logDir, "audit.jsonl");
    }

    public override string Name => "AuditModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        switch (command.Action.ToLowerInvariant())
        {
            case "getauditlogs":
                await HandleGetAuditLogsAsync(command, context).ConfigureAwait(false);
                return true;
            case "clearauditlogs":
                await HandleClearAuditLogsAsync(command, context).ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    public async Task LogCommandAsync(AgentCommand command, AgentContext context, bool success, string? error = null)
    {
        var entry = new AuditEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Action = command.Action,
            NodeId = command.NodeId ?? "unknown",
            SessionId = command.SessionId ?? "unknown",
            UserRights = context.UserRights.ToString(),
            Success = success,
            Error = error,
            MachineName = Environment.MachineName,
            UserName = Environment.UserName
        };

        _auditLog.Enqueue(entry);

        // Limit in-memory queue
        while (_auditLog.Count > MaxInMemoryEntries)
        {
            _auditLog.TryDequeue(out _);
        }

        // Write to file asynchronously
        await WriteAuditEntryToFileAsync(entry).ConfigureAwait(false);
    }

    private async Task WriteAuditEntryToFileAsync(AuditEntry entry)
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.AppendAllTextAsync(_auditLogPath, json + Environment.NewLine).ConfigureAwait(false);

            // Rotate log if too large
            var fileInfo = new FileInfo(_auditLogPath);
            if (fileInfo.Exists && fileInfo.Length > 10 * 1024 * 1024) // 10 MB
            {
                await RotateAuditLogAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to write audit entry to file");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private Task RotateAuditLogAsync()
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var archivePath = Path.Combine(
                Path.GetDirectoryName(_auditLogPath)!,
                $"audit_{timestamp}.jsonl");

            File.Move(_auditLogPath, archivePath);

            // Keep only last 5 archives
            var archiveDir = Path.GetDirectoryName(_auditLogPath)!;
            var archives = Directory.GetFiles(archiveDir, "audit_*.jsonl")
                .OrderByDescending(f => f)
                .Skip(5)
                .ToList();

            foreach (var archive in archives)
            {
                File.Delete(archive);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to rotate audit log");
        }

        return Task.CompletedTask;
    }

    private async Task HandleGetAuditLogsAsync(AgentCommand command, AgentContext context)
    {
        var maxEntries = command.Payload.TryGetProperty("maxEntries", out var maxElement) && maxElement.ValueKind == JsonValueKind.Number
            ? maxElement.GetInt32()
            : 100;

        var logs = new JsonArray();

        // Get from in-memory queue first
        var inMemory = _auditLog.Reverse().Take(maxEntries).ToList();
        foreach (var entry in inMemory)
        {
            logs.Add(ConvertAuditEntryToJson(entry));
        }

        // If we need more, read from file
        if (logs.Count < maxEntries)
        {
            var fromFile = await ReadAuditLogsFromFileAsync(maxEntries - logs.Count).ConfigureAwait(false);
            foreach (var entry in fromFile)
            {
                logs.Add(entry);
            }
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject
            {
                ["logs"] = logs,
                ["count"] = logs.Count
            })).ConfigureAwait(false);
    }

    private async Task<List<JsonObject>> ReadAuditLogsFromFileAsync(int maxEntries)
    {
        var entries = new List<JsonObject>();

        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_auditLogPath))
            {
                return entries;
            }

            var lines = await File.ReadAllLinesAsync(_auditLogPath).ConfigureAwait(false);
            var relevantLines = lines.Reverse().Take(maxEntries);

            foreach (var line in relevantLines)
            {
                try
                {
                    var json = JsonSerializer.Deserialize<JsonObject>(line);
                    if (json != null)
                    {
                        entries.Add(json);
                    }
                }
                catch
                {
                    // Skip invalid lines
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read audit logs from file");
        }
        finally
        {
            _fileLock.Release();
        }

        return entries;
    }

    private async Task HandleClearAuditLogsAsync(AgentCommand command, AgentContext context)
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _auditLog.Clear();

            if (File.Exists(_auditLogPath))
            {
                File.Delete(_auditLogPath);
            }

            Logger.LogWarning("Audit logs cleared");

            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.CommandId,
                command.NodeId,
                command.SessionId,
                new JsonObject { ["cleared"] = true })).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private static JsonObject ConvertAuditEntryToJson(AuditEntry entry)
    {
        return new JsonObject
        {
            ["timestamp"] = entry.Timestamp.ToString("O"),
            ["action"] = entry.Action,
            ["nodeId"] = entry.NodeId,
            ["sessionId"] = entry.SessionId,
            ["userRights"] = entry.UserRights,
            ["success"] = entry.Success,
            ["error"] = entry.Error,
            ["machineName"] = entry.MachineName,
            ["userName"] = entry.UserName
        };
    }

    private sealed class AuditEntry
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Action { get; set; } = "";
        public string NodeId { get; set; } = "";
        public string SessionId { get; set; } = "";
        public string UserRights { get; set; } = "";
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string MachineName { get; set; } = "";
        public string UserName { get; set; } = "";
    }

    public override async ValueTask DisposeAsync()
    {
        _fileLock.Dispose();
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
