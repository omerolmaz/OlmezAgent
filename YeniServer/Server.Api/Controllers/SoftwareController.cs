using Microsoft.AspNetCore.Mvc;
using Server.Application.Interfaces;
using Server.Application.DTOs.Command;
using Server.Api.Services;
using System.Text.Json;

namespace Server.Api.Controllers;

/// <summary>
/// Yazılım dağıtımı ve yönetimi
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SoftwareController : ControllerBase
{
    private readonly ICommandService _commandService;
    private readonly AgentConnectionManager _connectionManager;
    private readonly ILogger<SoftwareController> _logger;

    public SoftwareController(
        ICommandService commandService,
        AgentConnectionManager connectionManager,
        ILogger<SoftwareController> logger)
    {
        _commandService = commandService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Yazılım kurulumu
    /// </summary>
    [HttpPost("install/{deviceId}")]
    public async Task<IActionResult> InstallSoftware(Guid deviceId, [FromBody] InstallRequest request)
    {
        return await ExecuteCommand(deviceId, "installsoftware", request);
    }

    /// <summary>
    /// Yazılım kaldırma
    /// </summary>
    [HttpPost("uninstall/{deviceId}")]
    public async Task<IActionResult> UninstallSoftware(Guid deviceId, [FromBody] UninstallRequest request)
    {
        return await ExecuteCommand(deviceId, "uninstallsoftware", request);
    }

    /// <summary>
    /// Windows güncellemelerini yükle
    /// </summary>
    [HttpPost("updates/install/{deviceId}")]
    public async Task<IActionResult> InstallUpdates(Guid deviceId, [FromBody] UpdateRequest? request = null)
    {
        return await ExecuteCommand(deviceId, "installupdates", request);
    }

    /// <summary>
    /// Yama planla
    /// </summary>
    [HttpPost("patch/schedule/{deviceId}")]
    public async Task<IActionResult> SchedulePatch(Guid deviceId, [FromBody] PatchRequest request)
    {
        return await ExecuteCommand(deviceId, "schedulepatch", request);
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

public class InstallRequest
{
    public string PackagePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
}

public class UninstallRequest
{
    public string ProductName { get; set; } = string.Empty;
}

public class UpdateRequest
{
    public string[]? UpdateIds { get; set; }
}

public class PatchRequest
{
    public string PatchUrl { get; set; } = string.Empty;
    public DateTime ScheduledTime { get; set; }
}
