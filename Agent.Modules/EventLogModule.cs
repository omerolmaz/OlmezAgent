using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Modules;

public sealed class EventLogModule : AgentModuleBase
{
    private static readonly IReadOnlyCollection<string> Actions = new[]
    {
        "geteventlogs",
        "getsecurityevents",
        "getapplicationevents",
        "getsystemevents",
        "starteventmonitor",
        "stopeventmonitor",
        "cleareventlog"
    };

    private readonly ConcurrentDictionary<string, EventLogWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<EventRecord>> _eventQueues = new(StringComparer.OrdinalIgnoreCase);

    public EventLogModule(ILogger<EventLogModule> logger) : base(logger)
    {
    }

    public override string Name => "EventLogModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        if (!OperatingSystem.IsWindows())
        {
            await SendNotImplementedAsync(command, context, "Event logs are only supported on Windows.").ConfigureAwait(false);
            return true;
        }

        switch (command.Action.ToLowerInvariant())
        {
            case "geteventlogs":
                await HandleGetEventLogsAsync(command, context).ConfigureAwait(false);
                return true;
            case "getsecurityevents":
                await HandleGetSecurityEventsAsync(command, context).ConfigureAwait(false);
                return true;
            case "getapplicationevents":
                await HandleGetApplicationEventsAsync(command, context).ConfigureAwait(false);
                return true;
            case "getsystemevents":
                await HandleGetSystemEventsAsync(command, context).ConfigureAwait(false);
                return true;
            case "starteventmonitor":
                await HandleStartEventMonitorAsync(command, context).ConfigureAwait(false);
                return true;
            case "stopeventmonitor":
                await HandleStopEventMonitorAsync(command, context).ConfigureAwait(false);
                return true;
            case "cleareventlog":
                await HandleClearEventLogAsync(command, context).ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleGetEventLogsAsync(AgentCommand command, AgentContext context)
    {
        var logName = command.Payload.TryGetProperty("logName", out var logElement) && logElement.ValueKind == JsonValueKind.String
            ? logElement.GetString()!
            : "Application";

        var maxEvents = command.Payload.TryGetProperty("maxEvents", out var maxElement) && maxElement.ValueKind == JsonValueKind.Number
            ? maxElement.GetInt32()
            : 100;

        var level = command.Payload.TryGetProperty("level", out var levelElement) && levelElement.ValueKind == JsonValueKind.String
            ? levelElement.GetString()
            : null;

        var hours = command.Payload.TryGetProperty("hours", out var hoursElement) && hoursElement.ValueKind == JsonValueKind.Number
            ? hoursElement.GetInt32()
            : 24;

        var events = await Task.Run(() => GetEventLogs(logName, maxEvents, level, hours)).ConfigureAwait(false);

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject
            {
                ["logName"] = logName,
                ["events"] = events,
                ["count"] = events.Count
            })).ConfigureAwait(false);
    }

    private static JsonArray GetEventLogs(string logName, int maxEvents, string? levelFilter, int hours)
    {
        var events = new JsonArray();

        try
        {
            var startTime = DateTime.Now.AddHours(-hours);
            var query = BuildEventQuery(logName, levelFilter, startTime);

            using var reader = new EventLogReader(new EventLogQuery(logName, PathType.LogName, query));

            int count = 0;
            EventRecord? eventRecord;

            while ((eventRecord = reader.ReadEvent()) != null && count < maxEvents)
            {
                using (eventRecord)
                {
                    events.Add(ConvertEventToJson(eventRecord));
                    count++;
                }
            }
        }
        catch (Exception ex)
        {
            events.Add(new JsonObject { ["error"] = $"Failed to read event log: {ex.Message}" });
        }

        return events;
    }

    private static string BuildEventQuery(string logName, string? levelFilter, DateTime startTime)
    {
        var timeString = startTime.ToUniversalTime().ToString("o");
        var query = $"*[System[TimeCreated[@SystemTime>='{timeString}']]]";

        if (!string.IsNullOrEmpty(levelFilter))
        {
            var level = levelFilter.ToLowerInvariant() switch
            {
                "critical" => "1",
                "error" => "2",
                "warning" => "3",
                "information" => "4",
                "verbose" => "5",
                _ => null
            };

            if (level != null)
            {
                query = $"*[System[TimeCreated[@SystemTime>='{timeString}'] and Level={level}]]";
            }
        }

        return query;
    }

    private static JsonObject ConvertEventToJson(EventRecord eventRecord)
    {
        var eventObj = new JsonObject
        {
            ["eventId"] = eventRecord.Id,
            ["level"] = eventRecord.Level.HasValue ? GetLevelName(eventRecord.Level.Value) : "Unknown",
            ["timeCreated"] = eventRecord.TimeCreated?.ToString("O"),
            ["source"] = eventRecord.ProviderName,
            ["computer"] = eventRecord.MachineName
        };

        try
        {
            eventObj["message"] = eventRecord.FormatDescription() ?? eventRecord.ToXml();
        }
        catch
        {
            eventObj["message"] = $"Event ID {eventRecord.Id} from {eventRecord.ProviderName}";
        }

        if (eventRecord.UserId != null)
        {
            eventObj["userId"] = eventRecord.UserId.Value;
        }

        if (eventRecord.ProcessId.HasValue)
        {
            eventObj["processId"] = eventRecord.ProcessId.Value;
        }

        if (eventRecord.ThreadId.HasValue)
        {
            eventObj["threadId"] = eventRecord.ThreadId.Value;
        }

        return eventObj;
    }

    private static string GetLevelName(byte level)
    {
        return level switch
        {
            1 => "Critical",
            2 => "Error",
            3 => "Warning",
            4 => "Information",
            5 => "Verbose",
            _ => $"Unknown ({level})"
        };
    }

    private async Task HandleGetSecurityEventsAsync(AgentCommand command, AgentContext context)
    {
        var payloadObject = new JsonObject { ["logName"] = "Security", ["maxEvents"] = 100, ["hours"] = 24 };
        var payloadElement = JsonSerializer.SerializeToElement(payloadObject);
        var modifiedCommand = new AgentCommand(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payloadElement,
            command.CancellationToken);
        await HandleGetEventLogsAsync(modifiedCommand, context).ConfigureAwait(false);
    }

    private async Task HandleGetApplicationEventsAsync(AgentCommand command, AgentContext context)
    {
        var payloadObject = new JsonObject { ["logName"] = "Application", ["maxEvents"] = 100, ["hours"] = 24 };
        var payloadElement = JsonSerializer.SerializeToElement(payloadObject);
        var modifiedCommand = new AgentCommand(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payloadElement,
            command.CancellationToken);
        await HandleGetEventLogsAsync(modifiedCommand, context).ConfigureAwait(false);
    }

    private async Task HandleGetSystemEventsAsync(AgentCommand command, AgentContext context)
    {
        var payloadObject = new JsonObject { ["logName"] = "System", ["maxEvents"] = 100, ["hours"] = 24 };
        var payloadElement = JsonSerializer.SerializeToElement(payloadObject);
        var modifiedCommand = new AgentCommand(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            payloadElement,
            command.CancellationToken);
        await HandleGetEventLogsAsync(modifiedCommand, context).ConfigureAwait(false);
    }

    private async Task HandleStartEventMonitorAsync(AgentCommand command, AgentContext context)
    {
        var logName = command.Payload.TryGetProperty("logName", out var logElement) && logElement.ValueKind == JsonValueKind.String
            ? logElement.GetString()!
            : "Application";

        var monitorId = command.SessionId ?? Guid.NewGuid().ToString();

        if (_watchers.ContainsKey(monitorId))
        {
            await SendNotImplementedAsync(command, context, $"Monitor '{monitorId}' already exists.").ConfigureAwait(false);
            return;
        }

        try
        {
            var query = new EventLogQuery(logName, PathType.LogName, "*");
            var watcher = new EventLogWatcher(query);
            var eventQueue = new ConcurrentQueue<EventRecord>();

            watcher.EventRecordWritten += (sender, args) =>
            {
                if (args.EventRecord != null)
                {
                    eventQueue.Enqueue(args.EventRecord);

                    // Limit queue size
                    while (eventQueue.Count > 1000)
                    {
                        eventQueue.TryDequeue(out _);
                    }
                }
            };

            watcher.Enabled = true;

            _watchers[monitorId] = watcher;
            _eventQueues[monitorId] = eventQueue;

            Logger.LogInformation("Event monitor started: {LogName} ({MonitorId})", logName, monitorId);

            await context.ResponseWriter.SendAsync(new CommandResult(
                command.Action,
                command.CommandId,
                command.NodeId,
                command.SessionId,
                new JsonObject
                {
                    ["monitorId"] = monitorId,
                    ["logName"] = logName,
                    ["started"] = true
                })).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start event monitor");
            await SendNotImplementedAsync(command, context, $"Failed to start monitor: {ex.Message}").ConfigureAwait(false);
        }
    }

    private async Task HandleStopEventMonitorAsync(AgentCommand command, AgentContext context)
    {
        var monitorId = command.SessionId ?? "";

        if (_watchers.TryRemove(monitorId, out var watcher))
        {
            watcher.Enabled = false;
            watcher.Dispose();
            _eventQueues.TryRemove(monitorId, out _);

            Logger.LogInformation("Event monitor stopped: {MonitorId}", monitorId);
        }

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.CommandId,
            command.NodeId,
            command.SessionId,
            new JsonObject
            {
                ["monitorId"] = monitorId,
                ["stopped"] = true
            })).ConfigureAwait(false);
    }

    private async Task HandleClearEventLogAsync(AgentCommand command, AgentContext context)
    {
        if (!command.Payload.TryGetProperty("logName", out var logElement) || logElement.ValueKind != JsonValueKind.String)
        {
            await SendNotImplementedAsync(command, context, "cleareventlog requires 'logName'.").ConfigureAwait(false);
            return;
        }

        var logName = logElement.GetString()!;
        var payload = new JsonObject { ["logName"] = logName };

        try
        {
            using var eventLog = new System.Diagnostics.EventLog(logName);
            eventLog.Clear();
            payload["cleared"] = true;

            Logger.LogWarning("Event log cleared: {LogName}", logName);
        }
        catch (Exception ex)
        {
            payload["error"] = ex.Message;
            Logger.LogError(ex, "Failed to clear event log: {LogName}", logName);
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

    public override async ValueTask DisposeAsync()
    {
        foreach (var watcher in _watchers.Values)
        {
            try
            {
                watcher.Enabled = false;
                watcher.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        _watchers.Clear();
        _eventQueues.Clear();

        await base.DisposeAsync().ConfigureAwait(false);
    }
}
