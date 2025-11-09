using Microsoft.AspNetCore.Mvc;
using Server.Api.Services;
using Server.Application.DTOs.Command;
using Server.Application.Interfaces;
using Server.Domain.Constants;
using System.Text.Json;

namespace Server.Api.Controllers;

/// <summary>
/// Agent PrivacyModule komutlarını HTTP üzerinden expose eder.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PrivacyController : ControllerBase
{
    private readonly ICommandService _commandService;
    private readonly AgentConnectionManager _connectionManager;
    private readonly ILogger<PrivacyController> _logger;

    public PrivacyController(
        ICommandService commandService,
        AgentConnectionManager connectionManager,
        ILogger<PrivacyController> logger)
    {
        _commandService = commandService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Privacy bar'ı göster.
    /// </summary>
    [HttpPost("bar/show/{deviceId:guid}")]
    public Task<IActionResult> ShowPrivacyBar(Guid deviceId, [FromBody] PrivacyBarRequest? request = null)
    {
        request ??= new PrivacyBarRequest();
        return ExecuteCommand(deviceId, AgentCommands.Categories.PrivacyBarShow, request);
    }

    /// <summary>
    /// Privacy bar'ı gizle.
    /// </summary>
    [HttpPost("bar/hide/{deviceId:guid}")]
    public Task<IActionResult> HidePrivacyBar(Guid deviceId)
    {
        return ExecuteCommand(deviceId, AgentCommands.Categories.PrivacyBarHide, null);
    }

    private async Task<IActionResult> ExecuteCommand(Guid deviceId, string commandType, object? parameters)
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
        {
            return BadRequest(new { error = result.ErrorMessage ?? "Failed to create command" });
        }

        var payload = new
        {
            action = commandType,
            commandId = result.Data.Id,
            timestamp = DateTime.UtcNow,
            parameters
        };

        var sent = await _connectionManager.SendCommandToAgentAsync(deviceId, payload);
        if (sent)
        {
            await _commandService.MarkCommandAsSentAsync(result.Data.Id);
            return Ok(result.Data);
        }

        _logger.LogWarning("Privacy command {Command} could not be delivered to device {DeviceId}", commandType, deviceId);
        return BadRequest(new { error = "Failed to send command" });
    }
}

public sealed class PrivacyBarRequest
{
    public string? Message { get; set; }
}
