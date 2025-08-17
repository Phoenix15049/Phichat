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

    public string? PhoneNumber { get; set; }   // E.164 like +98912...
    public bool PhoneVerified { get; set; } = false;
}
