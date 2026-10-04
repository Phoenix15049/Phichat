namespace Phichat.Domain.Entities;

/// <summary>
/// A rotating refresh token. Only the SHA-256 hash of the token is stored.
/// Tokens issued by successive refreshes share a <see cref="FamilyId"/>, so reuse of an
/// already-rotated token (a sign of theft) can revoke the whole chain.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Hash of the token that replaced this one during rotation, if any.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public User User { get; set; } = default!;

    public bool IsActive(DateTime utcNow) => RevokedAtUtc == null && ExpiresAtUtc > utcNow;
}
