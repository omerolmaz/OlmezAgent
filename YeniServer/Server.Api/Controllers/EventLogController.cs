using Microsoft.AspNetCore.Mvc;
using Server.Application.Interfaces;
using Server.Application.DTOs.Command;
using Server.Api.Services;
using System.Text.Json;

namespace Server.Api.Controllers;

/// <summary>
/// Event Log yönetimi
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EventLogController : ControllerBase
{
    private readonly ICommandService _commandService;
    private readonly AgentConnectionManager _connectionManager;
    private readonly ILogger<EventLogController> _logger;

    public EventLogController(
        ICommandService commandService,
        AgentConnectionManager connectionManager,
        ILogger<EventLogController> logger)
    {
        _commandService = commandService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Event log kayıtları
    /// </summary>
    [HttpPost("get/{deviceId}")]
    public async Task<IActionResult> GetEventLogs(Guid deviceId, [FromBody] EventLogRequest request)
    {
        return await ExecuteCommand(deviceId, "geteventlogs", request);
    }

    /// <summary>
    /// Güvenlik olayları
    /// </summary>
    [HttpPost("security/{deviceId}")]
    public async Task<IActionResult> GetSecurityEvents(Guid deviceId, [FromBody] EventLogRequest? request = null)
    {
        return await ExecuteCommand(deviceId, "getsecurityevents", request);
    }

    /// <summary>
    /// Uygulama olayları
    /// </summary>
    [HttpPost("application/{deviceId}")]
    public async Task<IActionResult> GetApplicationEvents(Guid deviceId, [FromBody] EventLogRequest? request = null)
    {
        return await ExecuteCommand(deviceId, "getapplicationevents", request);
    }

    /// <summary>
    /// Sistem olayları
    /// </summary>
    [HttpPost("system/{deviceId}")]
    public async Task<IActionResult> GetSystemEvents(Guid deviceId, [FromBody] EventLogRequest? request = null)
    {
        return await ExecuteCommand(deviceId, "getsystemevents", request);
    }

    /// <summary>
    /// Event log izlemeyi başlat
    /// </summary>
    [HttpPost("monitor/start/{deviceId}")]
    public async Task<IActionResult> StartEventMonitor(Guid deviceId, [FromBody] EventMonitorRequest request)
    {
        return await ExecuteCommand(deviceId, "starteventmonitor", request);
    }

    /// <summary>
    /// Event log izlemeyi durdur
    /// </summary>
    [HttpPost("monitor/stop/{deviceId}")]
    public async Task<IActionResult> StopEventMonitor(Guid deviceId, [FromBody] StopMonitorRequest request)
    {
        return await ExecuteCommand(deviceId, "stopeventmonitor", request);
    }

    /// <summary>
    /// Event log'u temizle
    /// </summary>
    [HttpPost("clear/{deviceId}")]
    public async Task<IActionResult> ClearEventLog(Guid deviceId, [FromBody] ClearLogRequest request)
    {
        return await ExecuteCommand(deviceId, "cleareventlog", request);
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

public class EventLogRequest
{
    public string? LogName { get; set; }
    public int? MaxEvents { get; set; }
    public DateTime? Since { get; set; }
}

public class EventMonitorRequest
{
    public string LogName { get; set; } = string.Empty;
    public string MonitorId { get; set; } = string.Empty;
}

public class StopMonitorRequest
{
    public string MonitorId { get; set; } = string.Empty;
}

public class ClearLogRequest
{
    public string LogName { get; set; } = string.Empty;
}
