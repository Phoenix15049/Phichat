using Phichat.Application.DTOs.Settings;

namespace Phichat.Application.Interfaces;

/// <summary>
/// Last seen and read receipt privacy. Presence is mutual: two users see each other's online
/// status and last seen only when each one's setting allows the other (and neither blocked the other).
/// </summary>
public interface IPrivacyService
{
    Task<PrivacySettingsDto> GetAsync(Guid userId);

    Task UpdateAsync(Guid userId, PrivacySettingsDto settings);

    Task<bool> CanSeePresenceAsync(Guid viewerId, Guid targetId);

    /// <summary>Related users (see <see cref="IUserService.GetRelatedUserIdsAsync"/>) who share presence with the user.</summary>
    Task<List<Guid>> GetPresenceAudienceAsync(Guid userId);

    /// <summary>Read receipts flow between two users only when both have them on.</summary>
    Task<bool> ReadReceiptsSharedAsync(Guid userA, Guid userB);
}
