using Microsoft.AspNetCore.Mvc;
using Server.Api.Services;
using Server.Application.DTOs.Command;
using Server.Application.Interfaces;
using Server.Domain.Constants;
using System.Text.Json;

namespace Server.Api.Controllers;

/// <summary>
/// FileMonitoringModule komutları için REST uçları.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FileMonitorController : ControllerBase
{
    private readonly ICommandService _commandService;
    private readonly AgentConnectionManager _connectionManager;
    private readonly ILogger<FileMonitorController> _logger;

    public FileMonitorController(
        ICommandService commandService,
        AgentConnectionManager connectionManager,
        ILogger<FileMonitorController> logger)
    {
        _commandService = commandService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    [HttpPost("start/{deviceId:guid}")]
    public Task<IActionResult> StartMonitor(Guid deviceId, [FromBody] StartFileMonitorRequest request)
        => ExecuteCommand(deviceId, AgentCommands.Categories.StartFileMonitor, request);

    [HttpPost("stop/{deviceId:guid}")]
    public Task<IActionResult> StopMonitor(Guid deviceId, [FromBody] StopFileMonitorRequest request)
        => ExecuteCommand(deviceId, AgentCommands.Categories.StopFileMonitor, request);

    [HttpPost("changes/{deviceId:guid}")]
    public Task<IActionResult> GetChanges(Guid deviceId, [FromBody] FileMonitorChangesRequest request)
        => ExecuteCommand(deviceId, AgentCommands.Categories.GetFileChanges, request);

    [HttpGet("{deviceId:guid}")]
    public Task<IActionResult> ListMonitors(Guid deviceId, [FromQuery] string? path = null)
    {
        object? parameters = string.IsNullOrWhiteSpace(path) ? null : new { path };
        return ExecuteCommand(deviceId, AgentCommands.Categories.ListMonitors, parameters);
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

        _logger.LogWarning("FileMonitor command {Command} could not be delivered to device {DeviceId}", commandType, deviceId);
        return BadRequest(new { error = "Failed to send command" });
    }
}

public sealed class StartFileMonitorRequest
{
    public string Path { get; set; } = string.Empty;
    public string[]? Filters { get; set; }
    public bool Recursive { get; set; } = true;
}

public sealed class StopFileMonitorRequest
{
    public string MonitorId { get; set; } = string.Empty;
}

public sealed class FileMonitorChangesRequest
{
    public string MonitorId { get; set; } = string.Empty;
}
