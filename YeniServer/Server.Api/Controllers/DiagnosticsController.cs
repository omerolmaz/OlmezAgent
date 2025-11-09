using Microsoft.AspNetCore.Mvc;
using Server.Application.Interfaces;
using Server.Application.DTOs.Command;
using Server.Api.Services;

namespace Server.Api.Controllers;

/// <summary>
/// Diagnostik komutları için API endpoint'leri
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DiagnosticsController : ControllerBase
{
    private readonly ICommandService _commandService;
    private readonly AgentConnectionManager _connectionManager;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(
        ICommandService commandService,
        AgentConnectionManager connectionManager,
        ILogger<DiagnosticsController> logger)
    {
        _commandService = commandService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Ping - Agent'a basit ping gönderir
    /// </summary>
    [HttpPost("ping/{deviceId}")]
    public async Task<IActionResult> Ping(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "ping", null);
    }

    /// <summary>
    /// Status - Agent durumunu alır
    /// </summary>
    [HttpPost("status/{deviceId}")]
    public async Task<IActionResult> GetStatus(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "status", null);
    }

    /// <summary>
    /// Agent Info - Agent bilgilerini alır
    /// </summary>
    [HttpPost("agentinfo/{deviceId}")]
    public async Task<IActionResult> GetAgentInfo(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "agentinfo", null);
    }

    /// <summary>
    /// Versions - Versiyon bilgilerini alır
    /// </summary>
    [HttpPost("versions/{deviceId}")]
    public async Task<IActionResult> GetVersions(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "versions", null);
    }

    /// <summary>
    /// Connection Details - Bağlantı detaylarını alır
    /// </summary>
    [HttpPost("connectiondetails/{deviceId}")]
    public async Task<IActionResult> GetConnectionDetails(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "connectiondetails", null);
    }

    private async Task<IActionResult> ExecuteCommand(Guid deviceId, string commandType, string? parameters)
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
            Parameters = parameters
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
