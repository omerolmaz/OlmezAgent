using Microsoft.AspNetCore.Mvc;
using Server.Api.Services;
using Server.Application.DTOs.Command;
using Server.Application.Interfaces;
using Server.Domain.Constants;
using System.Text.Json;

namespace Server.Api.Controllers;

/// <summary>
/// MaintenanceModule komutları için REST uçları.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MaintenanceController : ControllerBase
{
    private readonly ICommandService _commandService;
    private readonly AgentConnectionManager _connectionManager;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(
        ICommandService commandService,
        AgentConnectionManager connectionManager,
        ILogger<MaintenanceController> logger)
    {
        _commandService = commandService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    [HttpPost("update/{deviceId:guid}")]
    public Task<IActionResult> TriggerUpdate(Guid deviceId, [FromBody] MaintenanceUpdateRequest request)
        => ExecuteCommand(deviceId, AgentCommands.Categories.AgentUpdate, request);

    [HttpPost("update-ex/{deviceId:guid}")]
    public Task<IActionResult> TriggerExtendedUpdate(Guid deviceId, [FromBody] MaintenanceUpdateExtendedRequest request)
        => ExecuteCommand(deviceId, AgentCommands.Categories.AgentUpdateEx, request);

    [HttpPost("reinstall/{deviceId:guid}")]
    public Task<IActionResult> Reinstall(Guid deviceId, [FromBody] MaintenanceReinstallRequest request)
        => ExecuteCommand(deviceId, AgentCommands.Categories.Reinstall, request);

    [HttpPost("logs/{deviceId:guid}")]
    public Task<IActionResult> CollectLogs(Guid deviceId, [FromBody] MaintenanceLogRequest request)
        => ExecuteCommand(deviceId, AgentCommands.Categories.Log, request);

    [HttpPost("download/{deviceId:guid}")]
    public Task<IActionResult> DownloadFile(Guid deviceId, [FromBody] MaintenanceDownloadRequest request)
        => ExecuteCommand(deviceId, AgentCommands.Categories.DownloadFile, request);

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

        _logger.LogWarning("Maintenance command {Command} could not be delivered to device {DeviceId}", commandType, deviceId);
        return BadRequest(new { error = "Failed to send command" });
    }
}

public class MaintenanceUpdateRequest
{
    public string? Version { get; set; }
    public bool Force { get; set; }
    public string? Channel { get; set; }
}

public class MaintenanceUpdateExtendedRequest : MaintenanceUpdateRequest
{
    public string? PackageUrl { get; set; }
    public string? Hash { get; set; }
    public string? Arguments { get; set; }
}

public sealed class MaintenanceReinstallRequest
{
    public string? InstallerUrl { get; set; }
    public bool PreserveConfig { get; set; } = true;
}

public sealed class MaintenanceLogRequest
{
    public int? TailLines { get; set; }
    public bool IncludeDiagnostics { get; set; }
}

public sealed class MaintenanceDownloadRequest
{
    public string SourceUrl { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
}
