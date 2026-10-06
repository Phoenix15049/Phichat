namespace Phichat.Domain.Entities;

/// <summary>
/// A group chat. Messages are end-to-end encrypted for the members at the time of sending
/// (one wrapped message key per member), so the server only knows who is in the group.
/// </summary>
public class Group
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class GroupMember
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public GroupRole Role { get; set; }
    public DateTime JoinedAtUtc { get; set; }

    /// <summary>Sent time of the newest message this member has read.</summary>
    public DateTime? LastReadAtUtc { get; set; }

    public Group Group { get; set; } = default!;
    public User User { get; set; } = default!;
}

public enum GroupRole
{
    Member = 0,
    Admin = 1,
    Owner = 2
}
