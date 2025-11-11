using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Application.Interfaces;
using Server.Application.DTOs.Command;
using Server.Domain.Enums;

namespace Server.Api.Controllers;

// [Authorize] // TODO: Enable when authentication is implemented
[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly ICommandService _commandService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(ICommandService commandService, ILogger<FilesController> logger)
    {
        _commandService = commandService;
        _logger = logger;
    }

    [HttpGet("{deviceId}/browse")]
    public async Task<IActionResult> BrowseFiles(string deviceId, [FromQuery] string path = "C:\\")
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new UnauthorizedAccessException());
            
            var commandRequest = new ExecuteCommandRequest
            {
                DeviceId = Guid.Parse(deviceId),
                CommandType = "file_browser",
                Parameters = System.Text.Json.JsonSerializer.Serialize(new
                {
                    operation = "list",
                    path = path
                })
            };

            var result = await _commandService.ExecuteCommandAsync(commandRequest, userId);

            if (result.Success)
            {
                return Ok(new { success = true, message = "Dosya listesi komutu gönderildi" });
            }

            return StatusCode(500, new { message = result.ErrorMessage ?? "Komut gönderilemedi" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing files for device {DeviceId}", deviceId);
            return StatusCode(500, new { message = "Dosya tarama sırasında hata oluştu" });
        }
    }

    [HttpGet("{deviceId}/drives")]
    public async Task<IActionResult> GetDrives(string deviceId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new UnauthorizedAccessException());
            
            var commandRequest = new ExecuteCommandRequest
            {
                DeviceId = Guid.Parse(deviceId),
                CommandType = "file_browser",
                Parameters = System.Text.Json.JsonSerializer.Serialize(new
                {
                    operation = "drives"
                })
            };

            var result = await _commandService.ExecuteCommandAsync(commandRequest, userId);

            if (result.Success)
            {
                return Ok(new { success = true, message = "Sürücü listesi komutu gönderildi" });
            }

            return StatusCode(500, new { message = result.ErrorMessage ?? "Komut gönderilemedi" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting drives for device {DeviceId}", deviceId);
            return StatusCode(500, new { message = "Sürücü listesi alınamadı" });
        }
    }
}
