using Microsoft.AspNetCore.Mvc;
using Server.Api.Services;
using Server.Application.DTOs.Command;
using Server.Application.Interfaces;
using Server.Domain.Constants;
using System.Text.Json;

namespace Server.Api.Controllers;

/// <summary>
/// JavaScriptBridge script yönetimi.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ScriptsController : ControllerBase
{
    private readonly ICommandService _commandService;
    private readonly AgentConnectionManager _connectionManager;
    private readonly ILogger<ScriptsController> _logger;

    public ScriptsController(
        ICommandService commandService,
        AgentConnectionManager connectionManager,
        ILogger<ScriptsController> logger)
    {
        _commandService = commandService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    [HttpPost("deploy/{deviceId:guid}")]
    public Task<IActionResult> Deploy(Guid deviceId, [FromBody] ScriptDeployRequest request)
        => ExecuteCommand(deviceId, AgentCommands.Categories.ScriptDeploy, request);

    [HttpPost("reload/{deviceId:guid}")]
    public Task<IActionResult> Reload(Guid deviceId)
        => ExecuteCommand(deviceId, AgentCommands.Categories.ScriptReload, null);

    [HttpGet("{deviceId:guid}")]
    public Task<IActionResult> List(Guid deviceId)
        => ExecuteCommand(deviceId, AgentCommands.Categories.ScriptList, null);

    [HttpDelete("{deviceId:guid}")]
    public Task<IActionResult> Remove(Guid deviceId, [FromBody] ScriptRemoveRequest request)
        => ExecuteCommand(deviceId, AgentCommands.Categories.ScriptRemove, request);

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

        _logger.LogWarning("Scripts command {Command} could not be delivered to device {DeviceId}", commandType, deviceId);
        return BadRequest(new { error = "Failed to send command" });
    }
}

public sealed class ScriptDeployRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? CodeBase64 { get; set; }
}

public sealed class ScriptRemoveRequest
{
    public string Name { get; set; } = string.Empty;
}
