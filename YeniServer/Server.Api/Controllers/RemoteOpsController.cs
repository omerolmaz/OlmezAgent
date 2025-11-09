using Microsoft.AspNetCore.Mvc;
using Server.Application.Interfaces;
using Server.Application.DTOs.Command;
using Server.Api.Services;
using System.Text.Json;

namespace Server.Api.Controllers;

/// <summary>
/// Uzaktan işlemler (power, service, file operations)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RemoteOpsController : ControllerBase
{
    private readonly ICommandService _commandService;
    private readonly AgentConnectionManager _connectionManager;
    private readonly ILogger<RemoteOpsController> _logger;

    public RemoteOpsController(
        ICommandService commandService,
        AgentConnectionManager connectionManager,
        ILogger<RemoteOpsController> logger)
    {
        _commandService = commandService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Güç yönetimi (restart, shutdown, sleep)
    /// </summary>
    [HttpPost("power/{deviceId}")]
    public async Task<IActionResult> PowerControl(Guid deviceId, [FromBody] PowerRequest request)
    {
        return await ExecuteCommand(deviceId, "power", request);
    }

    /// <summary>
    /// Windows servisi yönetimi
    /// </summary>
    [HttpPost("service/{deviceId}")]
    public async Task<IActionResult> ServiceControl(Guid deviceId, [FromBody] ServiceRequest request)
    {
        return await ExecuteCommand(deviceId, "service", request);
    }

    /// <summary>
    /// Dosya/klasör listeleme
    /// </summary>
    [HttpPost("ls/{deviceId}")]
    public async Task<IActionResult> ListFiles(Guid deviceId, [FromBody] FileListRequest request)
    {
        return await ExecuteCommand(deviceId, "ls", request);
    }

    /// <summary>
    /// Klasör oluşturma
    /// </summary>
    [HttpPost("mkdir/{deviceId}")]
    public async Task<IActionResult> MakeDirectory(Guid deviceId, [FromBody] FileOpRequest request)
    {
        return await ExecuteCommand(deviceId, "mkdir", request);
    }

    /// <summary>
    /// Dosya/klasör silme
    /// </summary>
    [HttpPost("rm/{deviceId}")]
    public async Task<IActionResult> Remove(Guid deviceId, [FromBody] FileOpRequest request)
    {
        return await ExecuteCommand(deviceId, "rm", request);
    }

    /// <summary>
    /// Dosya sıkıştırma
    /// </summary>
    [HttpPost("zip/{deviceId}")]
    public async Task<IActionResult> Zip(Guid deviceId, [FromBody] ZipRequest request)
    {
        return await ExecuteCommand(deviceId, "zip", request);
    }

    /// <summary>
    /// Dosya açma
    /// </summary>
    [HttpPost("unzip/{deviceId}")]
    public async Task<IActionResult> Unzip(Guid deviceId, [FromBody] ZipRequest request)
    {
        return await ExecuteCommand(deviceId, "unzip", request);
    }

    /// <summary>
    /// URL açma
    /// </summary>
    [HttpPost("openurl/{deviceId}")]
    public async Task<IActionResult> OpenUrl(Guid deviceId, [FromBody] UrlRequest request)
    {
        return await ExecuteCommand(deviceId, "openurl", request);
    }

    /// <summary>
    /// Wake on LAN
    /// </summary>
    [HttpPost("wakeonlan/{deviceId}")]
    public async Task<IActionResult> WakeOnLan(Guid deviceId, [FromBody] WolRequest request)
    {
        return await ExecuteCommand(deviceId, "wakeonlan", request);
    }

    /// <summary>
    /// Clipboard içeriğini al
    /// </summary>
    [HttpPost("clipboard/get/{deviceId}")]
    public async Task<IActionResult> GetClipboard(Guid deviceId)
    {
        return await ExecuteCommand(deviceId, "clipboardget");
    }

    /// <summary>
    /// Clipboard içeriğini ayarla
    /// </summary>
    [HttpPost("clipboard/set/{deviceId}")]
    public async Task<IActionResult> SetClipboard(Guid deviceId, [FromBody] ClipboardRequest request)
    {
        return await ExecuteCommand(deviceId, "clipboardset", request);
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

// Request models
public class PowerRequest
{
    public string Action { get; set; } = string.Empty; // restart, shutdown, sleep
}

public class ServiceRequest
{
    public string Action { get; set; } = string.Empty; // start, stop, restart
    public string ServiceName { get; set; } = string.Empty;
}

public class FileListRequest
{
    public string Path { get; set; } = string.Empty;
}

public class FileOpRequest
{
    public string Path { get; set; } = string.Empty;
}

public class ZipRequest
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
}

public class UrlRequest
{
    public string Url { get; set; } = string.Empty;
}

public class WolRequest
{
    public string MacAddress { get; set; } = string.Empty;
}

public class ClipboardRequest
{
    public string Content { get; set; } = string.Empty;
}
