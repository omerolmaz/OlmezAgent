using Microsoft.AspNetCore.Mvc;
using Server.Application.DTOs.Command;
using Server.Application.Interfaces;
using Server.Api.Services;

namespace Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommandsController : ControllerBase
{
    private readonly ICommandService _commandService;
    private readonly AgentConnectionManager _connectionManager;
    private readonly ILogger<CommandsController> _logger;

    public CommandsController(
        ICommandService commandService, 
        AgentConnectionManager connectionManager,
        ILogger<CommandsController> logger)
    {
        _commandService = commandService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Get command by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _commandService.GetCommandByIdAsync(id);
        if (!result.Success)
            return NotFound(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Get commands for a device
    /// </summary>
    [HttpGet("device/{deviceId}")]
    public async Task<IActionResult> GetByDevice(Guid deviceId)
    {
        var result = await _commandService.GetCommandsByDeviceAsync(deviceId);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Execute command on device (sends via WebSocket)
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] ExecuteCommandRequest request)
    {
        try
        {
            // Agent'ın bağlı olup olmadığını kontrol et
            if (!_connectionManager.IsConnected(request.DeviceId))
            {
                return BadRequest(new { error = $"Device {request.DeviceId} is not connected" });
            }

            // For now, use a default user ID (in production, get from authentication)
            var userId = Guid.Parse("00000000-0000-0000-0000-000000000001");

            var result = await _commandService.ExecuteCommandAsync(request, userId);
            if (!result.Success || result.Data == null)
                return BadRequest(new { error = result.ErrorMessage ?? "Failed to create command", code = result.ErrorCode });

            // WebSocket üzerinden komutu agent'a gönder
            var command = new
            {
                action = request.CommandType,
                commandId = result.Data.Id,
                timestamp = DateTime.UtcNow,
                parameters = request.Parameters
            };

            var success = await _connectionManager.SendCommandToAgentAsync(request.DeviceId, command);

            if (success)
            {
                // Command gönderildi olarak işaretle
                await _commandService.MarkCommandAsSentAsync(result.Data.Id);
                
                _logger.LogInformation("Command {CommandId} sent to device {DeviceId}", result.Data.Id, request.DeviceId);
                return Ok(result.Data);
            }
            else
            {
                return BadRequest(new { error = "Failed to send command to agent" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command on device {DeviceId}", request.DeviceId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Bağlı agent'ların listesini döner
    /// </summary>
    [HttpGet("active")]
    public ActionResult GetActiveConnections()
    {
        try
        {
            var connectedDevices = _connectionManager.GetConnectedDevices();
            var count = connectedDevices.Count;

            return Ok(new
            {
                success = true,
                data = new
                {
                    connectedCount = count,
                    deviceIds = connectedDevices,
                    timestamp = DateTime.UtcNow
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active connections");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Belirli bir agent'ın bağlı olup olmadığını kontrol eder
    /// </summary>
    [HttpGet("status/{deviceId}")]
    public ActionResult GetDeviceConnectionStatus(Guid deviceId)
    {
        try
        {
            var isConnected = _connectionManager.IsConnected(deviceId);

            return Ok(new
            {
                success = true,
                data = new
                {
                    deviceId,
                    isConnected,
                    timestamp = DateTime.UtcNow
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking device status {DeviceId}", deviceId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
