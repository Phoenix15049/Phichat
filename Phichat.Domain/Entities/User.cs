namespace Phichat.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

    public string? PhoneNumber { get; set; }   //like +98912...
    public bool PhoneVerified { get; set; } = false;

    /// <summary>Who may see this user's online status and last seen time.</summary>
    public PrivacyLevel LastSeenVisibility { get; set; } = PrivacyLevel.Everyone;

    /// <summary>When off, read receipts are neither sent nor shown in private chats.</summary>
    public bool ReadReceiptsEnabled { get; set; } = true;
}

public enum PrivacyLevel
{
    Everyone = 0,
    Contacts = 1,
    Nobody = 2
}
