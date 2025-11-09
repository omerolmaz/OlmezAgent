using Microsoft.AspNetCore.Mvc;
using Server.Application.Interfaces;
using Server.Application.DTOs.Command;
using Server.Api.Services;
using System.Text.Json;

namespace Server.Api.Controllers;

/// <summary>
/// Sağlık kontrolü ve metrikler
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ICommandService _commandService;
    private readonly AgentConnectionManager _connectionManager;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        ICommandService commandService,
        AgentConnectionManager connectionManager,
        ILogger<HealthController> logger)
    {
        _commandService = commandService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Sağlık durumu
    /// </summary>
    [HttpPost("check/{deviceId}")]
    public async Task<IActionResult> HealthCheck(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "health");
    }

    /// <summary>
    /// Metrikler
    /// </summary>
    [HttpPost("metrics/{deviceId}")]
    public async Task<IActionResult> GetMetrics(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "metrics");
    }

    /// <summary>
    /// Çalışma süresi
    /// </summary>
    [HttpPost("uptime/{deviceId}")]
    public async Task<IActionResult> GetUptime(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "uptime");
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
