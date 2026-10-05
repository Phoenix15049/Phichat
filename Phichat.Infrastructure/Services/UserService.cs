using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.User;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;

namespace Phichat.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl,
                LastSeenUtc = u.LastSeenUtc
            })
            .FirstOrDefaultAsync();
    }

    public Task<UserProfileDto?> GetProfileByUsernameAsync(string username) =>
        _context.Users
            .AsNoTracking()
            .Where(x => x.Username == username)
            .Select(x => new UserProfileDto
            {
                Id = x.Id,
                Username = x.Username,
                DisplayName = x.DisplayName,
                AvatarUrl = x.AvatarUrl,
                Bio = x.Bio,
                LastSeenUtc = x.LastSeenUtc
            })
            .FirstOrDefaultAsync();

    public Task<UserProfileDto?> GetMyProfileAsync(Guid userId) =>
        _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new UserProfileDto
            {
                Id = x.Id,
                Username = x.Username,
                DisplayName = x.DisplayName,
                AvatarUrl = x.AvatarUrl,
                Bio = x.Bio,
                LastSeenUtc = x.LastSeenUtc,
                PhoneNumber = x.PhoneNumber
            })
            .FirstOrDefaultAsync();

    public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _context.Users.FindAsync(userId)
            ?? throw new NotFoundException("user_not_found", "User not found.");

        user.DisplayName = NullIfEmpty(request.DisplayName);
        user.AvatarUrl = NormalizeAvatarUrl(request.AvatarUrl, userId);
        user.Bio = NullIfEmpty(request.Bio);

        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsUsernameAvailableAsync(string username) =>
        !await _context.Users.AnyAsync(x => x.Username == username);

    public async Task UpdateLastSeenAsync(Guid userId, DateTime utcNow)
    {
        await _context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeenUtc, utcNow));
    }

    public async Task<List<Guid>> GetRelatedUserIdsAsync(Guid userId)
    {
        var related = await _context.Messages.Where(m => m.SenderId == userId).Select(m => m.ReceiverId)
            .Union(_context.Messages.Where(m => m.ReceiverId == userId).Select(m => m.SenderId))
            .Union(_context.Contacts.Where(c => c.OwnerId == userId).Select(c => c.ContactId))
            .Union(_context.Contacts.Where(c => c.ContactId == userId).Select(c => c.OwnerId))
            .ToListAsync();

        related.Remove(userId);

        // Blocked pairs (either way) share no presence or last seen.
        var blocked = await _context.UserBlocks
            .Where(b => b.BlockerId == userId || b.BlockedId == userId)
            .Select(b => b.BlockerId == userId ? b.BlockedId : b.BlockerId)
            .ToListAsync();
        related.RemoveAll(blocked.Contains);

        return related;
    }

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// Only avatars uploaded by this user through <c>POST /api/users/avatar</c> are accepted, so a profile
    /// cannot point viewers' browsers at an arbitrary external URL. Stored as a server-relative path.
    /// </summary>
    private static string? NormalizeAvatarUrl(string? value, Guid userId)
    {
        var url = NullIfEmpty(value);
        if (url == null) return null;

        var path = Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https"
            ? absolute.AbsolutePath
            : url;

        var pattern = $@"^/uploads/avatars/{userId}/[0-9]{{17}}\.(jpe?g|png|webp|gif)$";
        if (!Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase))
            throw new BadRequestException("invalid_avatar_url", "Avatar must be an image uploaded to your profile.");

        return path;
    }
}
