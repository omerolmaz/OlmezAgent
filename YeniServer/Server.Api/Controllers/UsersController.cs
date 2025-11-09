using Microsoft.AspNetCore.Mvc;
using Server.Api.Attributes;
using Server.Application.DTOs.User;
using Server.Application.Interfaces;

namespace Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Login
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _userService.LoginAsync(request);
        if (!result.Success)
            return Unauthorized(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Get all users (requires authentication)
    /// </summary>
    [HttpGet]
    [RequireAuth]
    public async Task<IActionResult> GetAll()
    {
        var result = await _userService.GetAllUsersAsync();
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Get user by ID (requires authentication)
    /// </summary>
    [HttpGet("{id}")]
    [RequireAuth]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _userService.GetUserByIdAsync(id);
        if (!result.Success)
            return NotFound(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Create new user (ADMIN ONLY - no public registration)
    /// </summary>
    [HttpPost]
    [RequireAdmin]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var result = await _userService.CreateUserAsync(request);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Update user (ADMIN ONLY)
    /// </summary>
    [HttpPut("{id}")]
    [RequireAdmin]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        request.Id = id;
        var result = await _userService.UpdateUserAsync(request);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(result.Data);
    }

    /// <summary>
    /// Delete user (ADMIN ONLY)
    /// </summary>
    [HttpDelete("{id}")]
    [RequireAdmin]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _userService.DeleteUserAsync(id);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });

        return Ok(new { message = "User deleted successfully" });
    }
}
