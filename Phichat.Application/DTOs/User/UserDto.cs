namespace Phichat.Application.DTOs.User;

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = default!;
    public DateTime? LastSeenUtc { get; set; }

    /// <summary>The user hides their last seen from the viewer (shown as "recently").</summary>
    public bool LastSeenHidden { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
}
