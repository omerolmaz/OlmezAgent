using Microsoft.AspNetCore.Mvc;
using Server.Application.Interfaces;
using Server.Application.DTOs.Command;
using Server.Api.Services;
using System.Text.Json;

namespace Server.Api.Controllers;

/// <summary>
/// Güvenlik izleme komutları
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SecurityController : ControllerBase
{
    private readonly ICommandService _commandService;
    private readonly AgentConnectionManager _connectionManager;
    private readonly ILogger<SecurityController> _logger;

    public SecurityController(
        ICommandService commandService,
        AgentConnectionManager connectionManager,
        ILogger<SecurityController> logger)
    {
        _commandService = commandService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Genel güvenlik durumu
    /// </summary>
    [HttpPost("status/{deviceId}")]
    public async Task<IActionResult> GetSecurityStatus(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "getsecuritystatus");
    }

    /// <summary>
    /// Antivirüs durumu
    /// </summary>
    [HttpPost("antivirus/{deviceId}")]
    public async Task<IActionResult> GetAntivirusStatus(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "getantivirusstatus");
    }

    /// <summary>
    /// Firewall durumu
    /// </summary>
    [HttpPost("firewall/{deviceId}")]
    public async Task<IActionResult> GetFirewallStatus(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "getfirewallstatus");
    }

    /// <summary>
    /// Windows Defender durumu
    /// </summary>
    [HttpPost("defender/{deviceId}")]
    public async Task<IActionResult> GetDefenderStatus(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "getdefenderstatus");
    }

    /// <summary>
    /// UAC durumu
    /// </summary>
    [HttpPost("uac/{deviceId}")]
    public async Task<IActionResult> GetUacStatus(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "getuacstatus");
    }

    /// <summary>
    /// Disk şifreleme durumu
    /// </summary>
    [HttpPost("encryption/{deviceId}")]
    public async Task<IActionResult> GetEncryptionStatus(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "getencryptionstatus");
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
