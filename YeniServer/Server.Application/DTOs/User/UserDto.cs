using Server.Domain.Enums;

namespace Server.Application.DTOs.User;

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public UserRights Rights { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public UserRights Rights { get; set; }
}

public class UpdateUserRequest
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public UserRights? Rights { get; set; }
    public bool? IsActive { get; set; }
}
