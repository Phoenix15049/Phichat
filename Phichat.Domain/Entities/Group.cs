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

    /// <summary>What ordinary members may do; admins and the owner may always do all of it.</summary>
    public GroupPermissions MemberPermissions { get; set; } = GroupPermissions.Default;

    /// <summary>Seconds a member waits between messages; 0 is off.</summary>
    public int SlowModeSeconds { get; set; }

    /// <summary>Lets anyone who has it join; null when the group has no invite link.</summary>
    public string? InviteCode { get; set; }
}

[Flags]
public enum GroupPermissions
{
    None = 0,
    SendMessages = 1,
    SendMedia = 2,
    PinMessages = 4,
    AddMembers = 8,
    ChangeInfo = 16,
    Default = SendMessages | SendMedia | PinMessages
}

/// <summary>Someone removed from a group: they cannot come back through its invite link.</summary>
public class GroupBan
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public Guid BannedById { get; set; }
    public DateTime BannedAtUtc { get; set; }

    public User User { get; set; } = default!;
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
