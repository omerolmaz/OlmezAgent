using Microsoft.AspNetCore.Mvc;
using Server.Application.Interfaces;
using Server.Application.DTOs.Command;
using Server.Api.Services;
using System.Text.Json;
using System.Collections.Concurrent;

namespace Server.Api.Controllers;

/// <summary>
/// Mesajlaşma ve bildirim komutları
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MessagingController : ControllerBase
{
    private readonly ICommandService _commandService;
    private readonly AgentConnectionManager _connectionManager;
    private readonly ILogger<MessagingController> _logger;
    private static readonly ConcurrentDictionary<Guid, List<ChatMessageDto>> _chatCache = new();

    public MessagingController(
        ICommandService commandService,
        AgentConnectionManager connectionManager,
        ILogger<MessagingController> logger)
    {
        _commandService = commandService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Bildirim göster (Toast)
    /// </summary>
    [HttpPost("notify/{deviceId}")]
    public async Task<IActionResult> ShowNotification(Guid deviceId, [FromBody] NotificationRequest request)
    {
        return await ExecuteCommand(deviceId, "notify", request);
    }

    /// <summary>
    /// Chat mesajı gönder
    /// </summary>
    [HttpPost("chat/{deviceId}")]
    public async Task<IActionResult> SendChat(Guid deviceId, [FromBody] ChatRequest request)
    {
        // sender alanını ekle
        var payload = new
        {
            message = request.Message,
            sender = request.FromUser ?? "Server",
            timestamp = DateTime.UtcNow
        };
        
        // Cache'e ekle
        var chatMsg = new ChatMessageDto
        {
            Sender = request.FromUser ?? "Server",
            Message = request.Message,
            Timestamp = DateTime.UtcNow.ToString("O")
        };
        _chatCache.AddOrUpdate(deviceId, 
            new List<ChatMessageDto> { chatMsg }, 
            (key, list) => { list.Add(chatMsg); return list; });
        
        return await ExecuteCommand(deviceId, "chat", payload);
    }

    /// <summary>
    /// Chat mesajlarını getir
    /// </summary>
    [HttpGet("chat/{deviceId}/messages")]
    public IActionResult GetChatMessages(Guid deviceId)
    {
        if (_chatCache.TryGetValue(deviceId, out var messages))
        {
            return Ok(messages);
        }
        return Ok(new List<ChatMessageDto>());
    }

    /// <summary>
    /// Agent'tan gelen chat mesajını cache'e ekle (internal kullanım)
    /// </summary>
    public static void AddAgentChatMessage(Guid deviceId, string sender, string message)
    {
        var chatMsg = new ChatMessageDto
        {
            Sender = sender,
            Message = message,
            Timestamp = DateTime.UtcNow.ToString("O")
        };
        _chatCache.AddOrUpdate(deviceId, 
            new List<ChatMessageDto> { chatMsg }, 
            (key, list) => { list.Add(chatMsg); return list; });
    }

    private async Task<IActionResult> ExecuteCommand(Guid deviceId, string commandType, object? parameters = null)
    {
        if (!_connectionManager.IsConnected(deviceId))
        {
            return BadRequest(new { error = "Device is not connected" });
        }

        var userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        
        var request = new ExecuteCommandRequest
        {
            DeviceId = deviceId,
            CommandType = commandType,
            Parameters = parameters != null ? JsonSerializer.Serialize(parameters) : null
        };

        var result = await _commandService.ExecuteCommandAsync(request, userId);
        if (!result.Success || result.Data == null)
            return BadRequest(new { error = result.ErrorMessage ?? "Failed to create command" });

        // Agent'a gönderilecek komut
        var command = new
        {
            action = commandType,
            commandId = result.Data.Id.ToString(),
            nodeId = deviceId.ToString(),
            sessionId = (string?)null,
            parameters = parameters ?? new { }
        };

        var sent = await _connectionManager.SendCommandToAgentAsync(deviceId, command);
        
        if (sent)
        {
            await _commandService.MarkCommandAsSentAsync(result.Data.Id);
            return Ok(result.Data);
        }

        return BadRequest(new { error = "Failed to send command" });
    }
}

public class NotificationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? FromUser { get; set; }
}

public class ChatMessageDto
{
    public string Sender { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}
