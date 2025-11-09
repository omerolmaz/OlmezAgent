using Microsoft.EntityFrameworkCore;
using Server.Application.Common;
using Server.Application.DTOs.User;
using Server.Application.Interfaces;
using Server.Domain.Entities;
using Server.Domain.Enums;
using Server.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace Server.Application.Services;

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;

    public UserService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null || !user.IsActive)
            return Result<LoginResponse>.Fail("Invalid username or password", "INVALID_CREDENTIALS");

        if (!VerifyPassword(request.Password, user.PasswordHash))
            return Result<LoginResponse>.Fail("Invalid username or password", "INVALID_CREDENTIALS");

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Generate JWT token (simplified for now)
        var token = GenerateToken(user);

        var response = new LoginResponse
        {
            Token = token,
            User = MapToDto(user)
        };

        return Result<LoginResponse>.Ok(response);
    }

    public async Task<Result<UserDto>> GetUserByIdAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return Result<UserDto>.Fail("User not found", "USER_NOT_FOUND");

        return Result<UserDto>.Ok(MapToDto(user));
    }

    public async Task<Result<List<UserDto>>> GetAllUsersAsync()
    {
        var users = await _context.Users
            .OrderBy(u => u.Username)
            .ToListAsync();

        var dtos = users.Select(MapToDto).ToList();
        return Result<List<UserDto>>.Ok(dtos);
    }

    public async Task<Result<UserDto>> CreateUserAsync(CreateUserRequest request)
    {
        // Check if username already exists
        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            return Result<UserDto>.Fail("Username already exists", "USERNAME_EXISTS");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            PasswordHash = HashPassword(request.Password),
            Email = request.Email,
            FullName = request.FullName,
            Rights = request.Rights,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Result<UserDto>.Ok(MapToDto(user));
    }

    public async Task<Result<UserDto>> UpdateUserAsync(UpdateUserRequest request)
    {
        var user = await _context.Users.FindAsync(request.Id);
        if (user == null)
            return Result<UserDto>.Fail("User not found", "USER_NOT_FOUND");

        if (request.Email != null)
            user.Email = request.Email;

        if (request.FullName != null)
            user.FullName = request.FullName;

        if (request.Rights.HasValue)
            user.Rights = request.Rights.Value;

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();
        return Result<UserDto>.Ok(MapToDto(user));
    }

    public async Task<Result> DeleteUserAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return Result.Fail("User not found", "USER_NOT_FOUND");

        // Prevent deleting the last admin
        if (user.Rights.HasFlag(UserRights.SystemAdmin))
        {
            var adminCount = await _context.Users
                .CountAsync(u => u.Rights.HasFlag(UserRights.SystemAdmin) && u.IsActive);

            if (adminCount <= 1)
                return Result.Fail("Cannot delete the last administrator", "LAST_ADMIN");
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Result.Ok();
    }

    public async Task<Result> UpdateLastLoginAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return Result.Fail("User not found", "USER_NOT_FOUND");

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Ok();
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashBytes);
    }

    private bool VerifyPassword(string password, string hash)
    {
        var passwordHash = HashPassword(password);
        return passwordHash == hash;
    }

    private string GenerateToken(User user)
    {
        // Simplified token generation (in production, use JWT)
        var tokenData = $"{user.Id}:{user.Username}:{DateTime.UtcNow.Ticks}";
        var tokenBytes = Encoding.UTF8.GetBytes(tokenData);
        return Convert.ToBase64String(tokenBytes);
    }

    private UserDto MapToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            Rights = user.Rights,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }
}
