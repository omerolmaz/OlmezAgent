using Microsoft.AspNetCore.Mvc;
using Server.Application.DTOs.License;
using Server.Application.Interfaces;
using Server.Domain.Enums;

namespace Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LicenseController : ControllerBase
{
    private readonly ILicenseService _licenseService;

    public LicenseController(ILicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    /// <summary>
    /// Get current active license
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCurrent()
    {
        var result = await _licenseService.GetCurrentLicenseAsync();
        if (!result.Success)
            return NotFound(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Validate license key
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateLicenseRequest request)
    {
        var result = await _licenseService.ValidateLicenseKeyAsync(request.LicenseKey);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Generate new license (admin only)
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateLicenseRequest request)
    {
        var result = await _licenseService.GenerateLicenseAsync(request);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Check if license has capacity for new device
    /// </summary>
    [HttpGet("capacity")]
    public async Task<IActionResult> CheckCapacity()
    {
        var result = await _licenseService.CheckDeviceCapacityAsync();
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(new { hasCapacity = result.Data });
    }

    /// <summary>
    /// Check if license has specific feature
    /// </summary>
    [HttpGet("feature/{feature}")]
    public async Task<IActionResult> CheckFeature(EnterpriseFeature feature)
    {
        var result = await _licenseService.HasFeatureAsync(feature);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(new { hasFeature = result.Data });
    }
}
