using Phichat.Application.DTOs.User;

namespace Phichat.Application.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetUserByIdAsync(Guid userId);
    Task UpdateLastSeenAsync(Guid userId, DateTime utcNow);
}
