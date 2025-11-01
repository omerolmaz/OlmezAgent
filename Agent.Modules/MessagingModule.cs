using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        "webrtcsdp",
        "webrtcice"
    };

    private readonly ConcurrentQueue<JsonObject> _agentMessages = new();
    private readonly ConcurrentQueue<JsonObject> _chatMessages = new();
    private readonly ConcurrentDictionary<string, JsonObject> _webrtcState = new(StringComparer.OrdinalIgnoreCase);

    public MessagingModule(ILogger<MessagingModule> logger)
        : base(logger)
    {
    }

    public override string Name => "MessagingModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
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

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            new JsonObject
            {
                ["ack"] = true,
                ["title"] = title,
                ["message"] = message
            })).ConfigureAwait(false);
    }

    private async Task HandleChatAsync(AgentCommand command, AgentContext context)
    {
        var payload = command.Payload;
        var sender = payload.TryGetProperty("sender", out var senderElement) && senderElement.ValueKind == JsonValueKind.String
            ? senderElement.GetString()
            : "unknown";
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

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            new JsonObject
            {
                ["ack"] = true,
                ["queueLength"] = _chatMessages.Count
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
}
