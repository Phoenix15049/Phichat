using Microsoft.EntityFrameworkCore;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Settings;
using Phichat.Application.Interfaces;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;

namespace Phichat.Infrastructure.Services;

public sealed class PrivacyService : IPrivacyService
{
    private readonly AppDbContext _db;
    private readonly IUserService _users;

    public PrivacyService(AppDbContext db, IUserService users)
    {
        _db = db;
        _users = users;
    }

    public async Task<PrivacySettingsDto> GetAsync(Guid userId)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.LastSeenVisibility, u.ReadReceiptsEnabled })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException("user_not_found", "User not found.");

        return new PrivacySettingsDto
        {
            LastSeen = ToName(user.LastSeenVisibility),
            ReadReceipts = user.ReadReceiptsEnabled
        };
    }

    public async Task UpdateAsync(Guid userId, PrivacySettingsDto settings)
    {
        var level = ParseLevel(settings.LastSeen);
        await _db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.LastSeenVisibility, level)
                .SetProperty(u => u.ReadReceiptsEnabled, settings.ReadReceipts));
    }

    public async Task<bool> CanSeePresenceAsync(Guid viewerId, Guid targetId)
    {
        if (viewerId == targetId) return true;

        if (await _db.UserBlocks.AnyAsync(b =>
                (b.BlockerId == viewerId && b.BlockedId == targetId) ||
                (b.BlockerId == targetId && b.BlockedId == viewerId)))
            return false;

        var levels = await _db.Users.AsNoTracking()
            .Where(u => u.Id == viewerId || u.Id == targetId)
            .Select(u => new { u.Id, u.LastSeenVisibility })
            .ToDictionaryAsync(u => u.Id, u => u.LastSeenVisibility);
        if (levels.Count != 2) return false;

        var contactPairs = await _db.Contacts.AsNoTracking()
            .Where(c => (c.OwnerId == viewerId && c.ContactId == targetId) || (c.OwnerId == targetId && c.ContactId == viewerId))
            .Select(c => c.OwnerId)
            .ToListAsync();

        return Allows(levels[targetId], contactPairs.Contains(targetId))
            && Allows(levels[viewerId], contactPairs.Contains(viewerId));
    }

    public async Task<List<Guid>> GetPresenceAudienceAsync(Guid userId)
    {
        var related = await _users.GetRelatedUserIdsAsync(userId);
        if (related.Count == 0) return related;

        var myLevel = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.LastSeenVisibility)
            .FirstOrDefaultAsync();
        if (myLevel == PrivacyLevel.Nobody) return new List<Guid>();

        var theirLevels = await _db.Users.AsNoTracking()
            .Where(u => related.Contains(u.Id))
            .Select(u => new { u.Id, u.LastSeenVisibility })
            .ToDictionaryAsync(u => u.Id, u => u.LastSeenVisibility);

        // Who I saved as a contact, and who saved me.
        var mine = (await _db.Contacts.AsNoTracking()
            .Where(c => c.OwnerId == userId && related.Contains(c.ContactId))
            .Select(c => c.ContactId)
            .ToListAsync()).ToHashSet();
        var theirs = (await _db.Contacts.AsNoTracking()
            .Where(c => c.ContactId == userId && related.Contains(c.OwnerId))
            .Select(c => c.OwnerId)
            .ToListAsync()).ToHashSet();

        return related
            .Where(id => theirLevels.TryGetValue(id, out var level)
                && Allows(myLevel, mine.Contains(id))
                && Allows(level, theirs.Contains(id)))
            .ToList();
    }

    public async Task<bool> ReadReceiptsSharedAsync(Guid userA, Guid userB)
    {
        if (userA == userB) return true;

        var enabled = await _db.Users.AsNoTracking()
            .CountAsync(u => (u.Id == userA || u.Id == userB) && u.ReadReceiptsEnabled);
        return enabled == 2;
    }

    /// <summary>Whether an owner with <paramref name="level"/> shows their presence to someone (in their contacts or not).</summary>
    private static bool Allows(PrivacyLevel level, bool viewerIsOwnersContact) =>
        level == PrivacyLevel.Everyone || (level == PrivacyLevel.Contacts && viewerIsOwnersContact);

    private static PrivacyLevel ParseLevel(string value) => value switch
    {
        "everyone" => PrivacyLevel.Everyone,
        "contacts" => PrivacyLevel.Contacts,
        "nobody" => PrivacyLevel.Nobody,
        _ => throw new BadRequestException("invalid_privacy_level", "LastSeen must be 'everyone', 'contacts' or 'nobody'.")
    };

    private static string ToName(PrivacyLevel level) => level switch
    {
        PrivacyLevel.Contacts => "contacts",
        PrivacyLevel.Nobody => "nobody",
        _ => "everyone"
    };
}
