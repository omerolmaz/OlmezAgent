$fullContent = @'
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Application.Interfaces;
using Server.Domain.Constants;
using Server.Domain.Entities;
using Server.Infrastructure.Data;
using System.Text.Json;

namespace Server.Api.Controllers;

[ApiController]
[Route("api/software")]
[Authorize]
public class SoftwareController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICommandService _commandService;
    private readonly ILogger<SoftwareController> _logger;

    public SoftwareController(
        ApplicationDbContext context,
        ICommandService commandService,
        ILogger<SoftwareController> logger)
    {
        _context = context;
        _commandService = commandService;
        _logger = logger;
    }

    [HttpGet("{deviceId}")]
    public async Task<IActionResult> GetInstalledSoftware(Guid deviceId)
    {
        var software = await _context.InstalledSoftware
            .Where(s => s.DeviceId == deviceId)
            .OrderBy(s => s.Name)
            .ToListAsync();
        return Ok(software);
    }

    [HttpPost("{deviceId}/refresh")]
    public async Task<IActionResult> RefreshSoftware(Guid deviceId)
    {
        try
        {
            var device = await _context.Devices.FindAsync(deviceId);
            if (device == null)
                return NotFound(new { message = "Device not found" });

            var commandId = await _commandService.SendCommandAsync(
                deviceId,
                AgentCommands.Categories.GetInstalledSoftware,
                new Dictionary<string, object>()
            );
            return Ok(new { message = "Software refresh requested", commandId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing software for device {DeviceId}", deviceId);
            return StatusCode(500, new { message = "Error refreshing software", error = ex.Message });
        }
    }

    [HttpPost("{deviceId}/install")]
    public async Task<IActionResult> InstallSoftware(Guid deviceId, [FromBody] InstallSoftwareRequest request)
    {
        try
        {
            var device = await _context.Devices.FindAsync(deviceId);
            if (device == null)
                return NotFound(new { message = "Device not found" });

            var pendingAction = new PendingAction
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ActionType = PendingActionType.SoftwareInstall,
                Status = PendingActionStatus.Pending,
                Details = JsonSerializer.Serialize(new { filePath = request.FilePath, arguments = request.Arguments, fileName = Path.GetFileName(request.FilePath) }),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "Unknown"
            };
            _context.PendingActions.Add(pendingAction);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.Empty.ToString()),
                DeviceId = deviceId,
                Action = "Install Software",
                EventType = "SoftwareInstall",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                Details = JsonSerializer.Serialize(new { filePath = request.FilePath, arguments = request.Arguments }),
                Success = true,
                Timestamp = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);

            var commandData = new Dictionary<string, object>
            {
                ["filePath"] = request.FilePath,
                ["arguments"] = request.Arguments ?? "",
                ["timeout"] = request.Timeout,
                ["runAsUser"] = request.RunAsUser,
                ["pendingActionId"] = pendingAction.Id.ToString()
            };

            var commandId = await _commandService.SendCommandAsync(deviceId, AgentCommands.Categories.InstallSoftware, commandData);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Software installation started for {Path.GetFileName(request.FilePath)}. Check pending actions for status.", pendingActionId = pendingAction.Id, commandId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing software on device {DeviceId}", deviceId);
            return StatusCode(500, new { message = "Error installing software", error = ex.Message });
        }
    }

    [HttpPost("{deviceId}/uninstall")]
    public async Task<IActionResult> UninstallSoftware(Guid deviceId, [FromBody] UninstallSoftwareRequest request)
    {
        try
        {
            var device = await _context.Devices.FindAsync(deviceId);
            if (device == null)
                return NotFound(new { message = "Device not found" });

            var pendingAction = new PendingAction
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ActionType = PendingActionType.SoftwareUninstall,
                Status = PendingActionStatus.Pending,
                Details = JsonSerializer.Serialize(new { name = request.Name, command = request.Command }),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "Unknown"
            };
            _context.PendingActions.Add(pendingAction);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.Empty.ToString()),
                DeviceId = deviceId,
                Action = $"Uninstall Software: {request.Name}",
                EventType = "SoftwareUninstall",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                Details = JsonSerializer.Serialize(new { name = request.Name, command = request.Command }),
                Success = true,
                Timestamp = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);

            var history = new AgentHistory
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                Type = AgentHistoryType.SoftwareUninstall,
                Command = request.Command,
                Username = User.Identity?.Name ?? "Unknown",
                CreatedAt = DateTime.UtcNow
            };
            _context.AgentHistories.Add(history);

            var commandData = new Dictionary<string, object>
            {
                ["name"] = request.Name,
                ["command"] = request.Command,
                ["timeout"] = request.Timeout,
                ["runAsUser"] = request.RunAsUser,
                ["pendingActionId"] = pendingAction.Id.ToString(),
                ["historyId"] = history.Id.ToString()
            };

            var commandId = await _commandService.SendCommandAsync(deviceId, AgentCommands.Categories.UninstallSoftware, commandData);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"{request.Name} will now be uninstalled on {device.Hostname}.", pendingActionId = pendingAction.Id, commandId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uninstalling software on device {DeviceId}", deviceId);
            return StatusCode(500, new { message = "Error uninstalling software", error = ex.Message });
        }
    }

    [HttpPost("{deviceId}/install-choco")]
    public async Task<IActionResult> InstallWithChoco(Guid deviceId, [FromBody] InstallChocoRequest request)
    {
        try
        {
            var device = await _context.Devices.FindAsync(deviceId);
            if (device == null)
                return NotFound(new { message = "Device not found" });

            var pendingAction = new PendingAction
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ActionType = PendingActionType.ChocoInstall,
                Status = PendingActionStatus.Pending,
                Details = JsonSerializer.Serialize(new { packageName = request.PackageName, version = request.Version }),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "Unknown"
            };
            _context.PendingActions.Add(pendingAction);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.Empty.ToString()),
                DeviceId = deviceId,
                Action = $"Install Chocolatey Package: {request.PackageName}",
                EventType = "SoftwareInstall",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                Details = JsonSerializer.Serialize(request),
                Success = true,
                Timestamp = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);

            var commandData = new Dictionary<string, object>
            {
                ["packageName"] = request.PackageName,
                ["version"] = request.Version ?? "",
                ["force"] = request.Force,
                ["pendingActionId"] = pendingAction.Id.ToString()
            };

            var commandId = await _commandService.SendCommandAsync(deviceId, AgentCommands.Categories.InstallWithChoco, commandData);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"{request.PackageName} will be installed shortly on {device.Hostname}. Check pending actions for status.", pendingActionId = pendingAction.Id, commandId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing Chocolatey package on device {DeviceId}", deviceId);
            return StatusCode(500, new { message = "Error installing package", error = ex.Message });
        }
    }

    [HttpPost("{deviceId}/install-chocolatey")]
    public async Task<IActionResult> InstallChocolatey(Guid deviceId)
    {
        try
        {
            var device = await _context.Devices.FindAsync(deviceId);
            if (device == null)
                return NotFound(new { message = "Device not found" });

            var commandId = await _commandService.SendCommandAsync(deviceId, AgentCommands.Categories.InstallChoco, new Dictionary<string, object>());
            return Ok(new { message = "Chocolatey installation started", commandId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing Chocolatey on device {DeviceId}", deviceId);
            return StatusCode(500, new { message = "Error installing Chocolatey", error = ex.Message });
        }
    }

    [HttpGet("{deviceId}/pending-actions")]
    public async Task<IActionResult> GetPendingActions(Guid deviceId)
    {
        var actions = await _context.PendingActions.Where(a => a.DeviceId == deviceId).OrderByDescending(a => a.CreatedAt).Take(50).ToListAsync();
        return Ok(actions);
    }

    [HttpGet("{deviceId}/history")]
    public async Task<IActionResult> GetAgentHistory(Guid deviceId, [FromQuery] int limit = 50)
    {
        var history = await _context.AgentHistories.Where(h => h.DeviceId == deviceId).OrderByDescending(h => h.CreatedAt).Take(limit).ToListAsync();
        return Ok(history);
    }
}

public class InstallSoftwareRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public int Timeout { get; set; } = 1800;
    public bool RunAsUser { get; set; }
}

public class UninstallSoftwareRequest
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public int Timeout { get; set; } = 1800;
    public bool RunAsUser { get; set; }
}

public class InstallChocoRequest
{
    public string PackageName { get; set; } = string.Empty;
    public string? Version { get; set; }
    public bool Force { get; set; } = true;
}
'@

$targetPath = "C:\Users\ÖMERÖLMEZ\OneDrive - SiteTelekom\Masaüstü\Yeni klasör\YeniServer\Server.Api\Controllers\SoftwareController.cs"
Set-Content -Path $targetPath -Value $fullContent -Encoding UTF8
Write-Host "SoftwareController.cs başarıyla oluşturuldu!" -ForegroundColor Green
