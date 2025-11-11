using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Application.Interfaces;
using Server.Domain.Constants;
using Server.Domain.Entities;
using Server.Infrastructure.Data;
using Server.Api.Services;
using System.Text.Json;
using System.Net.WebSockets;
using System.Text;
using System.Security.Cryptography;

namespace Server.Api.Controllers;

[ApiController]
[Route("api/software")]
// [Authorize] // TODO: Enable when authentication is implemented
public class SoftwareController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICommandService _commandService;
    private readonly ILogger<SoftwareController> _logger;
    private readonly AgentConnectionManager _agentConnectionManager;
    private readonly string _uploadPath;

    public SoftwareController(
        ApplicationDbContext context,
        ICommandService commandService,
        ILogger<SoftwareController> logger,
        AgentConnectionManager agentConnectionManager,
        IWebHostEnvironment environment)
    {
        _context = context;
        _commandService = commandService;
        _logger = logger;
        _agentConnectionManager = agentConnectionManager;
        
        // Dosyaların saklanacağı dizin (meshcentral-files gibi)
        _uploadPath = Path.Combine(environment.ContentRootPath, "software-files");
        if (!Directory.Exists(_uploadPath))
        {
            Directory.CreateDirectory(_uploadPath);
            _logger.LogInformation("Created software-files directory at: {Path}", _uploadPath);
        }
    }

    private async Task<Guid> SendDeviceCommandAsync(Guid deviceId, string commandType, Dictionary<string, object>? parameters = null)
    {
        // Get userId from claims, use null if not authenticated
        var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst("sub")?.Value;
        Guid? userId = null;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedUserId))
        {
            userId = parsedUserId;
        }
        
        var parametersJson = parameters != null && parameters.Count > 0 ? JsonSerializer.Serialize(parameters) : null;
        
        var request = new Server.Application.DTOs.Command.ExecuteCommandRequest
        {
            DeviceId = deviceId,
            CommandType = commandType,
            Parameters = parametersJson
        };

        var result = await _commandService.ExecuteCommandAsync(request, userId);
        if (result.Success && result.Data != null)
        {
            var commandId = result.Data.Id;
            
            // Send command to agent via WebSocket
            var commandMessage = new
            {
                action = commandType,
                commandId = commandId,
                timestamp = DateTime.UtcNow,
                parameters = parameters
            };
            
            await _agentConnectionManager.SendCommandToAgentAsync(deviceId, commandMessage);
            
            return commandId;
        }
        
        throw new Exception(result.ErrorMessage ?? "Failed to execute command");
    }

    /// <summary>
    /// Dosya adına göre otomatik sessiz kurulum parametrelerini belirler
    /// </summary>
    private string GetDefaultInstallArguments(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLower();
        var fileNameLower = fileName.ToLower();

        // MSI dosyaları
        if (extension == ".msi")
        {
            return "/qn /norestart"; // Quiet mode, no UI, no restart
        }

        // EXE dosyaları - dosya adına göre tahmin
        if (extension == ".exe")
        {
            // Inno Setup (çoğu modern installer)
            if (fileNameLower.Contains("setup") || fileNameLower.Contains("install"))
            {
                return "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES";
            }
            
            // 7-Zip, WinRAR gibi
            if (fileNameLower.Contains("7z") || fileNameLower.Contains("winrar"))
            {
                return "/S";
            }

            // NSIS installer
            if (fileNameLower.Contains("nsis"))
            {
                return "/S";
            }

            // Varsayılan: En yaygın parametreler
            return "/S"; // NSIS style silent
        }

        return string.Empty;
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

            var commandId = await SendDeviceCommandAsync(
                deviceId,
                AgentCommands.Categories.GetInstalledSoftware
            );
            return Ok(new { message = "Software refresh requested", commandId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing software for device {DeviceId}", deviceId);
            return StatusCode(500, new { message = "Error refreshing software", error = ex.Message });
        }
    }

    /// <summary>
    /// Yazılım kurulumu - Üç mod destekler:
    /// 1. FilePath ile: Zaten agent'ta bulunan dosya
    /// 2. DownloadUrl ile: Agent URL'den indirir ve kurar
    /// 3. FileId ile: Server'a yüklenen dosyayı agent indirir ve kurar
    /// </summary>
    [HttpPost("{deviceId}/install")]
    public async Task<IActionResult> InstallSoftware(Guid deviceId, [FromBody] InstallSoftwareRequest request)
    {
        try
        {
            var device = await _context.Devices.FindAsync(deviceId);
            if (device == null)
                return NotFound(new { message = "Device not found" });

            if (device.Status != Server.Domain.Enums.ConnectionStatus.Connected)
                return BadRequest(new { message = "Agent is not online" });

            // Validate: Either FilePath, DownloadUrl, or FileId must be provided
            if (string.IsNullOrWhiteSpace(request.FilePath) && 
                string.IsNullOrWhiteSpace(request.DownloadUrl) && 
                string.IsNullOrWhiteSpace(request.FileId))
                return BadRequest(new { message = "Either FilePath, DownloadUrl, or FileId is required" });

            string fileName;
            string mode;
            string? downloadUrl = null;
            
            if (!string.IsNullOrWhiteSpace(request.FileId))
            {
                // Mode: Download from server (uploaded file)
                mode = "ServerFile";
                
                // FileId'den dosyayı bul
                var exeFile = Path.Combine(_uploadPath, $"{request.FileId}.exe");
                var msiFile = Path.Combine(_uploadPath, $"{request.FileId}.msi");
                
                if (System.IO.File.Exists(exeFile))
                {
                    fileName = request.FileName ?? $"{request.FileId}.exe";
                }
                else if (System.IO.File.Exists(msiFile))
                {
                    fileName = request.FileName ?? $"{request.FileId}.msi";
                }
                else
                {
                    return NotFound(new { message = "Uploaded file not found" });
                }
                
                // Server'daki dosyanın URL'ini oluştur (agent buradan indirecek)
                var serverUrl = $"{Request.Scheme}://{Request.Host}";
                downloadUrl = $"{serverUrl}/api/software/files/{request.FileId}";
                
                _logger.LogInformation("Install from server file: FileId={FileId}, DownloadUrl={Url}", 
                    request.FileId, downloadUrl);
            }
            else if (!string.IsNullOrWhiteSpace(request.DownloadUrl))
            {
                // Mode: Download from URL
                mode = "URL";
                downloadUrl = request.DownloadUrl;
                fileName = request.FileName ?? Path.GetFileName(new Uri(request.DownloadUrl).LocalPath);
                
                var extension = Path.GetExtension(fileName).ToLower();
                if (extension != ".exe" && extension != ".msi")
                    return BadRequest(new { message = "Only .exe and .msi files are supported" });
            }
            else
            {
                // Mode: Install from existing file path
                mode = "FilePath";
                fileName = Path.GetFileName(request.FilePath);
            }

            var pendingAction = new PendingAction
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ActionType = PendingActionType.SoftwareInstall,
                Status = PendingActionStatus.Pending,
                Details = JsonSerializer.Serialize(new { 
                    mode = mode,
                    filePath = request.FilePath,
                    downloadUrl = downloadUrl,
                    fileId = request.FileId,
                    fileName = fileName,
                    arguments = request.Arguments 
                }),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "Unknown"
            };
            _context.PendingActions.Add(pendingAction);

            var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst("sub")?.Value;
            Guid? installUserId = null;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedUserId))
            {
                installUserId = parsedUserId;
            }

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = installUserId,
                DeviceId = deviceId,
                Action = $"Install Software ({mode})",
                EventType = "SoftwareInstall",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                Details = JsonSerializer.Serialize(new { 
                    mode = mode,
                    filePath = request.FilePath,
                    downloadUrl = downloadUrl,
                    fileId = request.FileId,
                    fileName = fileName,
                    arguments = request.Arguments 
                }),
                Success = true,
                Timestamp = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();

            // Otomatik parametre ekleme: Eğer kullanıcı parametre girmediyse, dosya tipine göre otomatik ekle
            var installArguments = request.Arguments;
            if (string.IsNullOrWhiteSpace(installArguments))
            {
                installArguments = GetDefaultInstallArguments(fileName);
                _logger.LogInformation("Auto-detected install arguments for {FileName}: {Arguments}", 
                    fileName, installArguments);
            }

            // Prepare command data based on mode
            var commandData = new Dictionary<string, object>
            {
                ["arguments"] = installArguments,
                ["timeout"] = request.Timeout,
                ["runAsUser"] = request.RunAsUser,
                ["pendingActionId"] = pendingAction.Id.ToString()
            };

            if (!string.IsNullOrWhiteSpace(downloadUrl))
            {
                // URL mode or ServerFile mode: Agent will download
                commandData["downloadUrl"] = downloadUrl;
                commandData["fileName"] = fileName;
            }
            else
            {
                // FilePath mode: Agent will use existing file
                commandData["filePath"] = request.FilePath;
            }

            var commandId = await SendDeviceCommandAsync(deviceId, AgentCommands.Categories.InstallSoftware, commandData);

            _logger.LogInformation($"Software installation started ({mode}): {fileName} on device {deviceId}");

            return Ok(new { 
                message = $"Software installation started for {fileName}",
                mode = mode,
                fileName = fileName,
                pendingActionId = pendingAction.Id, 
                commandId 
            });
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

            // Get userId from claims, use null if not authenticated
            var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst("sub")?.Value;
            Guid? userId = null;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId, // nullable - null if not authenticated
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

            // Save PendingAction, AuditLog, and AgentHistory first
            await _context.SaveChangesAsync();

            var commandData = new Dictionary<string, object>
            {
                ["softwareName"] = request.Name,
                ["uninstallString"] = request.Command,
                ["timeout"] = request.Timeout,
                ["runAsUser"] = request.RunAsUser,
                ["pendingActionId"] = pendingAction.Id.ToString(),
                ["historyId"] = history.Id.ToString()
            };

            // SendDeviceCommandAsync calls SaveChangesAsync internally
            var commandId = await SendDeviceCommandAsync(deviceId, AgentCommands.Categories.UninstallSoftware, commandData);

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

            // Get userId from claims, use null if not authenticated
            var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst("sub")?.Value;
            Guid? chocoUserId = null;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedUserId))
            {
                chocoUserId = parsedUserId;
            }

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = chocoUserId,
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

            // Save PendingAction and AuditLog first
            await _context.SaveChangesAsync();

            var commandData = new Dictionary<string, object>
            {
                ["packageName"] = request.PackageName,
                ["version"] = request.Version ?? "",
                ["force"] = request.Force,
                ["pendingActionId"] = pendingAction.Id.ToString()
            };

            // SendDeviceCommandAsync calls SaveChangesAsync internally
            var commandId = await SendDeviceCommandAsync(deviceId, AgentCommands.Categories.InstallWithChoco, commandData);

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

            var commandId = await SendDeviceCommandAsync(deviceId, AgentCommands.Categories.InstallChoco, new Dictionary<string, object>());
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

    /// <summary>
    /// Dosya yükleme endpoint'i - Frontend'den dosya alır ve server'da saklar
    /// </summary>
    [HttpPost("upload-file")]
    [RequestSizeLimit(500_000_000)] // 500MB limit
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });

            // Dosya uzantısı kontrolü
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (extension != ".exe" && extension != ".msi")
                return BadRequest(new { message = "Only .exe and .msi files are supported" });

            // Güvenli dosya adı oluştur (hash + orijinal uzantı)
            var fileId = Guid.NewGuid().ToString("N");
            var safeFileName = $"{fileId}{extension}";
            var filePath = Path.Combine(_uploadPath, safeFileName);

            // Dosyayı kaydet
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("File uploaded: {FileName} -> {FileId}, Size: {Size} bytes", 
                file.FileName, fileId, file.Length);

            return Ok(new 
            { 
                message = "File uploaded successfully",
                fileId = fileId,
                fileName = file.FileName,
                size = file.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file");
            return StatusCode(500, new { message = "Error uploading file", error = ex.Message });
        }
    }

    /// <summary>
    /// Dosya indirme endpoint'i - Agent yüklenen dosyayı buradan indirir
    /// </summary>
    [HttpGet("files/{fileId}")]
    public async Task<IActionResult> DownloadFile(string fileId)
    {
        try
        {
            // Güvenlik: Sadece GUID formatında fileId kabul et
            if (!Guid.TryParse(fileId.Replace(".exe", "").Replace(".msi", ""), out _))
                return BadRequest(new { message = "Invalid file ID" });

            // .exe veya .msi uzantılı dosyayı bul
            var exeFile = Path.Combine(_uploadPath, $"{fileId}.exe");
            var msiFile = Path.Combine(_uploadPath, $"{fileId}.msi");
            
            string filePath;
            string contentType;
            
            if (System.IO.File.Exists(exeFile))
            {
                filePath = exeFile;
                contentType = "application/x-msdownload";
            }
            else if (System.IO.File.Exists(msiFile))
            {
                filePath = msiFile;
                contentType = "application/x-msi";
            }
            else
            {
                return NotFound(new { message = "File not found" });
            }

            _logger.LogInformation("File download requested: {FileId}", fileId);

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            return File(memory, contentType, Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {FileId}", fileId);
            return StatusCode(500, new { message = "Error downloading file", error = ex.Message });
        }
    }
}

public class InstallSoftwareRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public int Timeout { get; set; } = 1800;
    public bool RunAsUser { get; set; }
    public string? DownloadUrl { get; set; } // URL'den indirme için
    public string? FileName { get; set; } // İndirilen dosya adı
    public string? FileId { get; set; } // Server'a yüklenen dosya ID'si
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
