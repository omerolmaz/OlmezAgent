using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Transport;

/// <summary>
/// WebSocket üzerinden CommandResult gönderen ResponseWriter
/// </summary>
public sealed class WebSocketResponseWriter : IAgentResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly Func<WebSocket?> _webSocketAccessor;
    private readonly ILogger<WebSocketResponseWriter> _logger;

    public WebSocketResponseWriter(Func<WebSocket?> webSocketAccessor, ILogger<WebSocketResponseWriter> logger)
    {
        _webSocketAccessor = webSocketAccessor;
        _logger = logger;
    }

    public async Task SendAsync(CommandResult result, CancellationToken cancellationToken = default)
    {
        var socket = _webSocketAccessor();
        if (socket == null || socket.State != WebSocketState.Open)
        {
            _logger.LogWarning("Cannot send message: WebSocket is not connected");
            return;
        }

        try
        {
            object responseEnvelope;
            
            // For protocol messages (agentinfo, register, heartbeat), send as-is with their action
            if (result.Action == "agentinfo" || result.Action == "register" || result.Action == "heartbeat" || result.Action == "agenthello")
            {
                responseEnvelope = result.Payload;
            }
            else
            {
                // For command results, wrap in commandresult envelope
                responseEnvelope = new
                {
                    action = "commandresult",
                    commandId = result.CommandId,
                    status = result.Success ? "Completed" : "Failed",
                    success = result.Success,
                    error = result.Error,
                    result = result.Payload
                };
            }

            var json = JsonSerializer.Serialize(responseEnvelope, SerializerOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            await socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken).ConfigureAwait(false);
            
            _logger.LogDebug("Sent message with action: {Action}, CommandId: {CommandId}", 
                result.Action, result.CommandId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message via WebSocket");
        }
    }
}
