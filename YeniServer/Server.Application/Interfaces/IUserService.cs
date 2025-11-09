using Server.Application.Common;
using Server.Application.DTOs.User;

namespace Server.Application.Interfaces;

public interface IUserService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request);
    Task<Result<UserDto>> GetUserByIdAsync(Guid id);
    Task<Result<List<UserDto>>> GetAllUsersAsync();
    Task<Result<UserDto>> CreateUserAsync(CreateUserRequest request);
    Task<Result<UserDto>> UpdateUserAsync(UpdateUserRequest request);
    Task<Result> DeleteUserAsync(Guid id);
    Task<Result> UpdateLastLoginAsync(Guid userId);
}
