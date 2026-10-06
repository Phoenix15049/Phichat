using Phichat.Application.DTOs.Settings;

namespace Phichat.Application.Interfaces;

/// <summary>Signed-in devices. A session is a refresh-token family; its id is the "sid" claim of access tokens.</summary>
public interface ISessionService
{
    Task<List<SessionDto>> GetSessionsAsync(Guid userId, Guid? currentSessionId);

    /// <summary>Ends one of the user's sessions.</summary>
    Task EndAsync(Guid userId, Guid sessionId);

    /// <summary>Ends every session of the user except <paramref name="keepSessionId"/>; returns the ended ids.</summary>
    Task<List<Guid>> EndOthersAsync(Guid userId, Guid keepSessionId);

    /// <summary>
    /// Revokes the session's refresh tokens, rejects its access tokens from now on and removes its push subscriptions.
    /// Returns false when nothing was active.
    /// </summary>
    Task<bool> RevokeSessionAsync(Guid sessionId);
}

/// <summary>Sessions ended before their access tokens expire; checked on every authenticated request.</summary>
public interface ISessionRevocationList
{
    void Revoke(Guid sessionId);
    bool IsRevoked(Guid sessionId);
}

/// <summary>The device making the current request, recorded for "Active sessions".</summary>
public interface IClientContext
{
    string? DeviceName { get; }
    string? IpAddress { get; }
}
