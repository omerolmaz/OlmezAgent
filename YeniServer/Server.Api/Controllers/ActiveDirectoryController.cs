using Microsoft.AspNetCore.Mvc;
using Server.Api.Attributes;
using Server.Application.Services;
using Server.Domain.Enums;

namespace Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireAdmin]
[RequiresFeature(EnterpriseFeature.ActiveDirectory)]
public class ActiveDirectoryController : ControllerBase
{
    private readonly IActiveDirectoryService _adService;
    private readonly ILogger<ActiveDirectoryController> _logger;

    public ActiveDirectoryController(
        IActiveDirectoryService adService,
        ILogger<ActiveDirectoryController> logger)
    {
        _adService = adService;
        _logger = logger;
    }

    /// <summary>
    /// Test Active Directory connection
    /// </summary>
    [HttpGet("test")]
    public async Task<IActionResult> TestConnection()
    {
        var result = await _adService.TestConnectionAsync();
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(new { connected = result.Data, message = "Active Directory connection successful" });
    }

    /// <summary>
    /// Get domain information
    /// </summary>
    [HttpGet("domain-info")]
    public async Task<IActionResult> GetDomainInfo()
    {
        var result = await _adService.GetDomainInfoAsync();
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Get users from Active Directory
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? filter = null)
    {
        var result = await _adService.GetUsersAsync(filter);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Get computers from Active Directory
    /// </summary>
    [HttpGet("computers")]
    public async Task<IActionResult> GetComputers([FromQuery] string? filter = null)
    {
        var result = await _adService.GetComputersAsync(filter);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Sync AD users to local database
    /// </summary>
    [HttpPost("sync/users")]
    public async Task<IActionResult> SyncUsers()
    {
        var result = await _adService.SyncUsersAsync();
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(new { success = true, message = "Users synced successfully" });
    }

    /// <summary>
    /// Sync AD computers to local database
    /// </summary>
    [HttpPost("sync/computers")]
    public async Task<IActionResult> SyncComputers()
    {
        var result = await _adService.SyncComputersAsync();
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(new { success = true, message = "Computers synced successfully" });
    }
}
