using Microsoft.AspNetCore.Mvc;
using Server.Application.Interfaces;
using Server.Application.DTOs.Command;
using Server.Api.Services;
using System.Text.Json;

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
    /// Agent mesajı gönder
    /// </summary>
    [HttpPost("agentmsg/{deviceId}")]
    public async Task<IActionResult> SendAgentMessage(Guid deviceId, [FromBody] MessageRequest request)
    {
        return await ExecuteCommand(deviceId, "agentmsg", request);
    }

    /// <summary>
    /// Windows message box göster
    /// </summary>
    [HttpPost("messagebox/{deviceId}")]
    public async Task<IActionResult> ShowMessageBox(Guid deviceId, [FromBody] MessageBoxRequest request)
    {
        return await ExecuteCommand(deviceId, "messagebox", request);
    }

    /// <summary>
    /// Bildirim göster
    /// </summary>
    [HttpPost("notify/{deviceId}")]
    public async Task<IActionResult> ShowNotification(Guid deviceId, [FromBody] NotificationRequest request)
    {
        return await ExecuteCommand(deviceId, "notify", request);
    }

    /// <summary>
    /// Toast bildirimi göster
    /// </summary>
    [HttpPost("toast/{deviceId}")]
    public async Task<IActionResult> ShowToast(Guid deviceId, [FromBody] ToastRequest request)
    {
        return await ExecuteCommand(deviceId, "toast", request);
    }

    /// <summary>
    /// Chat mesajı gönder
    /// </summary>
    [HttpPost("chat/{deviceId}")]
    public async Task<IActionResult> SendChat(Guid deviceId, [FromBody] ChatRequest request)
    {
        return await ExecuteCommand(deviceId, "chat", request);
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

        var command = new
        {
            action = commandType,
            commandId = result.Data.Id,
            timestamp = DateTime.UtcNow,
            parameters
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

public class MessageRequest
{
    public string Message { get; set; } = string.Empty;
}

public class MessageBoxRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "info"; // info, warning, error
}

public class NotificationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ToastRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Duration { get; set; } = 5000; // ms
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? FromUser { get; set; }
}
