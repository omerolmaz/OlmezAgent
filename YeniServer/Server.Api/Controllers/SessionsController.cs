using Microsoft.AspNetCore.Mvc;
using Server.Application.DTOs.Session;
using Server.Application.Interfaces;
using Server.Api.Services;

namespace Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly AgentConnectionManager _connectionManager;
    private readonly ICommandService _commandService;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        ISessionService sessionService,
        AgentConnectionManager connectionManager,
        ICommandService commandService,
        ILogger<SessionsController> logger)
    {
        _sessionService = sessionService;
        _connectionManager = connectionManager;
        _commandService = commandService;
        _logger = logger;
    }

    /// <summary>
    /// Desktop session başlatır
    /// </summary>
    [HttpPost("desktop/start")]
    public async Task<IActionResult> StartDesktopSession([FromBody] StartSessionRequest request)
    {
        _logger.LogInformation("StartDesktopSession called: DeviceId={DeviceId}, SessionType={SessionType}", 
            request?.DeviceId, request?.SessionType);

        if (request == null)
        {
            _logger.LogWarning("StartDesktopSession: Request is null");
            return BadRequest(new { error = "Request body is required" });
        }

        if (request.DeviceId == Guid.Empty)
        {
            _logger.LogWarning("StartDesktopSession: DeviceId is empty");
            return BadRequest(new { error = "DeviceId is required" });
        }

        // Agent bağlı mı kontrol et
        if (!_connectionManager.IsConnected(request.DeviceId))
        {
            _logger.LogWarning("StartDesktopSession: Device {DeviceId} is not connected", request.DeviceId);
            return BadRequest(new { error = "Device is not connected" });
        }

        // Session oluştur
        var userId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // TODO: Auth'dan al
        request.SessionType = "desktop";
        var sessionResult = await _sessionService.StartSessionAsync(request, userId);

        if (!sessionResult.Success || sessionResult.Data == null)
        {
            _logger.LogError("StartDesktopSession: Failed to create session - {Error}", sessionResult.ErrorMessage);
            return BadRequest(new { error = sessionResult.ErrorMessage ?? "Failed to start session" });
        }

        // Agent'a desktopstart komutu gönder
        // InitialData'dan quality ve fps parametrelerini parse et
        int quality = 60;
        int fps = 15;
        
        if (!string.IsNullOrEmpty(sessionResult.Data.SessionData))
        {
            try
            {
                var initialData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(sessionResult.Data.SessionData);
                if (initialData != null)
                {
                    if (initialData.ContainsKey("quality")) 
                        quality = Convert.ToInt32(initialData["quality"]);
                    if (initialData.ContainsKey("fps")) 
                        fps = Convert.ToInt32(initialData["fps"]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse session initial data");
            }
        }

        var command = new
        {
            action = "desktopstart",
            sessionId = sessionResult.Data.SessionId,
            payload = new
            {
                quality = quality,
                fps = fps
            },
            timestamp = DateTime.UtcNow
        };

        var sent = await _connectionManager.SendCommandToAgentAsync(request.DeviceId, command);

        if (!sent)
        {
            _logger.LogError("StartDesktopSession: Failed to send command to agent {DeviceId}", request.DeviceId);
            return BadRequest(new { error = "Failed to send command to agent" });
        }

        _logger.LogInformation("StartDesktopSession: Session started successfully - SessionId={SessionId}", 
            sessionResult.Data.SessionId);
        return Ok(new { success = true, data = sessionResult.Data });
    }

    /// <summary>
    /// Desktop session'ı durdurur
    /// </summary>
    [HttpPost("desktop/stop")]
    public async Task<IActionResult> StopDesktopSession([FromBody] EndSessionRequest request)
    {
        _logger.LogInformation("StopDesktopSession called: SessionId={SessionId}", request?.SessionId);

        if (request == null || request.SessionId == Guid.Empty)
        {
            return BadRequest(new { error = "Valid SessionId is required" });
        }

        var userId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // TODO: Auth'dan al
        
        // Session bilgisini al
        var sessionResult = await _sessionService.GetSessionAsync(request.SessionId);
        if (!sessionResult.Success || sessionResult.Data == null)
        {
            _logger.LogWarning("StopDesktopSession: Session not found - {SessionId}", request.SessionId);
            return NotFound(new { error = sessionResult.ErrorMessage ?? "Session not found" });
        }

        // Agent'a desktopstop komutu gönder
        var command = new
        {
            action = "desktopstop",
            sessionId = sessionResult.Data.SessionId,
            timestamp = DateTime.UtcNow
        };

        await _connectionManager.SendCommandToAgentAsync(sessionResult.Data.DeviceId, command);

        // Session'ı sonlandır
        var endResult = await _sessionService.EndSessionAsync(request.SessionId, userId);

        _logger.LogInformation("StopDesktopSession: Session stopped - {SessionId}", request.SessionId);
        return Ok(new { success = endResult.Success });
    }

    /// <summary>
    /// Desktop frame alır
    /// </summary>
    [HttpPost("desktop/frame")]
    public async Task<IActionResult> GetDesktopFrame([FromQuery] Guid sessionId)
    {
        var sessionResult = await _sessionService.GetSessionAsync(sessionId);
        if (!sessionResult.Success || sessionResult.Data == null)
        {
            return NotFound(new { error = sessionResult.ErrorMessage ?? "Session not found" });
        }

        var session = sessionResult.Data;
        if (!session.IsActive)
        {
            return BadRequest(new { error = "Session is not active" });
        }

        // Agent'a frame isteği gönder
        var command = new
        {
            action = "desktopframe",
            sessionId = session.SessionId,
            timestamp = DateTime.UtcNow
        };

        var sent = await _connectionManager.SendCommandToAgentAsync(session.DeviceId, command);

        if (!sent)
        {
            return BadRequest(new { error = "Failed to send command to agent" });
        }

        // Session activity güncelle
        await _sessionService.UpdateSessionActivityAsync(sessionId);

        return Ok(new { success = true, message = "Frame request sent" });
    }

    /// <summary>
    /// Console session başlatır
    /// </summary>
    [HttpPost("console/start")]
    public async Task<IActionResult> StartConsoleSession([FromBody] StartSessionRequest request)
    {
        _logger.LogInformation("StartConsoleSession called: DeviceId={DeviceId}", request?.DeviceId);

        if (request == null || request.DeviceId == Guid.Empty)
        {
            return BadRequest(new { error = "Valid DeviceId is required" });
        }

        if (!_connectionManager.IsConnected(request.DeviceId))
        {
            return BadRequest(new { error = "Device is not connected" });
        }

        var userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        request.SessionType = "console";
        var sessionResult = await _sessionService.StartSessionAsync(request, userId);

        if (!sessionResult.Success || sessionResult.Data == null)
        {
            _logger.LogError("StartConsoleSession: Failed - {Error}", sessionResult.ErrorMessage);
            return BadRequest(new { error = sessionResult.ErrorMessage ?? "Failed to start session" });
        }

        // Agent'a console komutu gönder
        var command = new
        {
            action = "console",
            sessionId = sessionResult.Data.SessionId,
            command = "start",
            timestamp = DateTime.UtcNow
        };

        var sent = await _connectionManager.SendCommandToAgentAsync(request.DeviceId, command);

        if (!sent)
        {
            return BadRequest(new { error = "Failed to send command to agent" });
        }

        return Ok(new { success = true, data = sessionResult.Data });
    }

    /// <summary>
    /// Console'a komut gönderir
    /// </summary>
    [HttpPost("console/execute")]
    public async Task<IActionResult> ExecuteConsoleCommand([FromBody] ConsoleCommandRequest request)
    {
        var sessionResult = await _sessionService.GetSessionAsync(request.SessionId);
        if (!sessionResult.Success || sessionResult.Data == null)
        {
            return NotFound(new { error = sessionResult.ErrorMessage ?? "Session not found" });
        }

        var session = sessionResult.Data;
        if (!session.IsActive || session.SessionType != "console")
        {
            return BadRequest(new { error = "Invalid or inactive console session" });
        }

        // Agent'a console komutu gönder
        var command = new
        {
            action = "console",
            sessionId = session.SessionId,
            command = request.Command,
            timestamp = DateTime.UtcNow
        };

        var sent = await _connectionManager.SendCommandToAgentAsync(session.DeviceId, command);

        if (!sent)
        {
            return BadRequest(new { error = "Failed to send command to agent" });
        }

        await _sessionService.UpdateSessionActivityAsync(request.SessionId);

        return Ok(new { success = true, message = "Command sent" });
    }

    /// <summary>
    /// Belirli bir device'ın aktif session'larını listeler
    /// </summary>
    [HttpGet("device/{deviceId}")]
    public async Task<IActionResult> GetDeviceSessions(Guid deviceId)
    {
        var result = await _sessionService.GetActiveSessionsAsync(deviceId);
        
        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Session bilgisini getirir
    /// </summary>
    [HttpGet("{sessionId}")]
    public async Task<IActionResult> GetSession(Guid sessionId)
    {
        var result = await _sessionService.GetSessionAsync(sessionId);
        
        if (!result.Success)
        {
            return NotFound(new { error = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Session'ı sonlandırır
    /// </summary>
    [HttpPost("{sessionId}/end")]
    public async Task<IActionResult> EndSession(Guid sessionId)
    {
        var userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var result = await _sessionService.EndSessionAsync(sessionId, userId);
        
        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { success = true });
    }
}

public class ConsoleCommandRequest
{
    public Guid SessionId { get; set; }
    public string Command { get; set; } = string.Empty;
}
