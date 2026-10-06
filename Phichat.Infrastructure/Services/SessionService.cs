using Microsoft.EntityFrameworkCore;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Settings;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;

namespace Phichat.Infrastructure.Services;

public sealed class SessionService : ISessionService
{
    private readonly AppDbContext _db;
    private readonly ISessionRevocationList _revocations;

    public SessionService(AppDbContext db, ISessionRevocationList revocations)
    {
        _db = db;
        _revocations = revocations;
    }

    public async Task<List<SessionDto>> GetSessionsAsync(Guid userId, Guid? currentSessionId)
    {
        var now = DateTime.UtcNow;

        // One active (not yet rotated) refresh token per session; its creation time is the last refresh.
        var tokens = await _db.RefreshTokens.AsNoTracking()
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null && t.ExpiresAtUtc > now)
            .Select(t => new { t.FamilyId, t.CreatedAtUtc, t.SessionStartedAtUtc, t.DeviceName, t.IpAddress })
            .ToListAsync();

        return tokens
            .GroupBy(t => t.FamilyId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(t => t.CreatedAtUtc).First();
                return new SessionDto
                {
                    Id = g.Key,
                    DeviceName = latest.DeviceName,
                    IpAddress = latest.IpAddress,
                    StartedAtUtc = latest.SessionStartedAtUtc == default ? g.Min(t => t.CreatedAtUtc) : latest.SessionStartedAtUtc,
                    LastActiveAtUtc = latest.CreatedAtUtc,
                    IsCurrent = g.Key == currentSessionId
                };
            })
            .OrderByDescending(s => s.IsCurrent)
            .ThenByDescending(s => s.LastActiveAtUtc)
            .ToList();
    }

    public async Task EndAsync(Guid userId, Guid sessionId)
    {
        var owned = await _db.RefreshTokens.AnyAsync(t => t.FamilyId == sessionId && t.UserId == userId && t.RevokedAtUtc == null);
        if (!owned)
            throw new NotFoundException("session_not_found", "This session has already ended.");

        await RevokeSessionAsync(sessionId);
    }

    public async Task<List<Guid>> EndOthersAsync(Guid userId, Guid keepSessionId)
    {
        var others = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null && t.FamilyId != keepSessionId)
            .Select(t => t.FamilyId)
            .Distinct()
            .ToListAsync();

        foreach (var sessionId in others)
            await RevokeSessionAsync(sessionId);

        return others;
    }

    public async Task<bool> RevokeSessionAsync(Guid sessionId)
    {
        var now = DateTime.UtcNow;

        var revoked = await _db.RefreshTokens
            .Where(t => t.FamilyId == sessionId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now));

        // Access tokens of the session stay valid until they expire unless they are rejected explicitly.
        _revocations.Revoke(sessionId);

        await _db.PushSubscriptions.Where(p => p.SessionId == sessionId).ExecuteDeleteAsync();

        return revoked > 0;
    }
}
