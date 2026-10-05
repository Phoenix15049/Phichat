namespace Phichat.Domain.Entities;

/// <summary><see cref="BlockerId"/> blocked <see cref="BlockedId"/>: no messages, typing or presence between them.</summary>
public class UserBlock
{
    public Guid BlockerId { get; set; }
    public Guid BlockedId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public User Blocker { get; set; } = default!;
    public User Blocked { get; set; } = default!;
}
