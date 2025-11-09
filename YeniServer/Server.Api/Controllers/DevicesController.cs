using Microsoft.AspNetCore.Mvc;
using Server.Application.DTOs.Device;
using Server.Application.Interfaces;

namespace Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;

    public DevicesController(IDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    /// <summary>
    /// Get all devices
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _deviceService.GetAllDevicesAsync();
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Get device by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _deviceService.GetDeviceByIdAsync(id);
        if (!result.Success)
            return NotFound(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Get devices by group
    /// </summary>
    [HttpGet("group/{groupId}")]
    public async Task<IActionResult> GetByGroup(Guid groupId)
    {
        var result = await _deviceService.GetDevicesByGroupAsync(groupId);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Register a new device (can also be called by agent)
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request)
    {
        var result = await _deviceService.RegisterDeviceAsync(request);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Delete device
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _deviceService.DeleteDeviceAsync(id);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(new { message = "Device deleted successfully" });
    }
}
