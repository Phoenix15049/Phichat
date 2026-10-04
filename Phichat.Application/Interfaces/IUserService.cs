using Phichat.Application.DTOs.User;

namespace Phichat.Application.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetUserByIdAsync(Guid userId);

    /// <summary>Public profile (no phone number).</summary>
    Task<UserProfileDto?> GetProfileByUsernameAsync(string username);

    /// <summary>The caller's own profile, including the phone number.</summary>
    Task<UserProfileDto?> GetMyProfileAsync(Guid userId);

    Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request);

    Task<bool> IsUsernameAvailableAsync(string username);
    Task UpdateLastSeenAsync(Guid userId, DateTime utcNow);

    /// <summary>
    /// Users who may see this user's presence: conversation partners and contacts (in either direction).
    /// </summary>
    Task<List<Guid>> GetRelatedUserIdsAsync(Guid userId);
}
