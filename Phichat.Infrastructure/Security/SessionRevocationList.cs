using Microsoft.Extensions.Caching.Memory;
using Phichat.Application.Interfaces;

namespace Phichat.Infrastructure.Security;

/// <summary>
/// In-memory list of ended sessions, kept for as long as their access tokens could still be valid.
/// Single-instance only: with several API instances this must move to a shared store (e.g. Redis).
/// </summary>
public sealed class SessionRevocationList : ISessionRevocationList
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _keepFor;

    public SessionRevocationList(IMemoryCache cache, ITokenService tokens)
    {
        _cache = cache;
        // Access token lifetime plus the validation clock skew.
        _keepFor = tokens.AccessTokenLifetime + TimeSpan.FromMinutes(1);
    }

    public void Revoke(Guid sessionId) => _cache.Set(Key(sessionId), true, _keepFor);

    public bool IsRevoked(Guid sessionId) => _cache.TryGetValue(Key(sessionId), out _);

    private static string Key(Guid sessionId) => "revoked-session:" + sessionId;
}
