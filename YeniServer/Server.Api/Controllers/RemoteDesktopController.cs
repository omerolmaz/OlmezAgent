using Microsoft.AspNetCore.Mvc;
using Server.Api.Services;

namespace Server.Api.Controllers;

[ApiController]
[Route("api/remote-desktop")]
public class RemoteDesktopController : ControllerBase
{
    private readonly AgentConnectionManager _connectionManager;
    private readonly ILogger<RemoteDesktopController> _logger;

    public RemoteDesktopController(AgentConnectionManager connectionManager, ILogger<RemoteDesktopController> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    [HttpPost("input/move")]
    public Task<IActionResult> SendMouseMove([FromBody] RemoteMouseMoveRequest request) =>
        SendRealtimeCommand(
            request.DeviceId,
            request.SessionId,
            "desktopmousemove",
            new { x = request.X, y = request.Y });

    [HttpPost("input/button")]
    public Task<IActionResult> SendMouseButton([FromBody] RemoteMouseButtonRequest request)
    {
        var action = request.Action?.ToLowerInvariant() switch
        {
            "down" => "desktopmousedown",
            "up" => "desktopmouseup",
            "click" or null => "desktopmouseclick",
            _ => "desktopmouseclick"
        };

        return SendRealtimeCommand(
            request.DeviceId,
            request.SessionId,
            action,
            new { button = request.Button });
    }

    [HttpPost("input/key")]
    public Task<IActionResult> SendKeyboard([FromBody] RemoteKeyRequest request)
    {
        var action = request.Action?.ToLowerInvariant() switch
        {
            "down" => "desktopkeydown",
            "up" => "desktopkeyup",
            "press" or null => "desktopkeypress",
            _ => "desktopkeypress"
        };

        return SendRealtimeCommand(
            request.DeviceId,
            request.SessionId,
            action,
            new { key = request.KeyCode });
    }

    private async Task<IActionResult> SendRealtimeCommand(Guid deviceId, string? sessionId, string action, object payload)
    {
        if (deviceId == Guid.Empty || string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { error = "deviceId and sessionId are required" });
        }

        if (!_connectionManager.IsConnected(deviceId))
        {
            return BadRequest(new { error = "Device is not connected" });
        }

        var envelope = new
        {
            action,
            commandId = Guid.NewGuid().ToString(),
            sessionId,
            parameters = payload,
            timestamp = DateTime.UtcNow
        };

        var sent = await _connectionManager.SendCommandToAgentAsync(deviceId, envelope);
        if (!sent)
        {
            _logger.LogWarning("Failed to send {Action} command to device {DeviceId}", action, deviceId);
            return BadRequest(new { error = "Failed to send command to agent" });
        }

        return Ok(new { success = true });
    }
}

public sealed class RemoteMouseMoveRequest
{
    public Guid DeviceId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class RemoteMouseButtonRequest
{
    public Guid DeviceId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int Button { get; set; }
    public string Action { get; set; } = "click";
}

public sealed class RemoteKeyRequest
{
    public Guid DeviceId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int KeyCode { get; set; }
    public string Action { get; set; } = "press";
}
