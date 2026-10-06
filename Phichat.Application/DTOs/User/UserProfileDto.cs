namespace Phichat.Application.DTOs.User;

public class UserProfileDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = default!;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public DateTime? LastSeenUtc { get; set; }

    /// <summary>The user hides their last seen from the viewer (shown as "recently").</summary>
    public bool LastSeenHidden { get; set; }
    public string? PhoneNumber { get; set; }
}
