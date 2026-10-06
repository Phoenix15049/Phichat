namespace Phichat.Application.DTOs.Group;

public class CreateGroupRequest
{
    public string Title { get; set; } = "";
    public List<Guid> MemberIds { get; set; } = new();
}

public class UpdateGroupRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
}

public class AddGroupMembersRequest
{
    public List<Guid> UserIds { get; set; } = new();
}

public class SetGroupRoleRequest
{
    /// <summary>"admin" or "member".</summary>
    public string Role { get; set; } = "member";
}

public class GroupMemberDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = default!;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>"owner", "admin" or "member".</summary>
    public string Role { get; set; } = "member";
    public DateTime JoinedAtUtc { get; set; }
}

public class GroupDetailsDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string MyRole { get; set; } = "member";
    public List<GroupMemberDto> Members { get; set; } = new();
}

/// <summary>A member's current identity key, for encrypting a group message.</summary>
public class GroupMemberKeyDto
{
    public Guid UserId { get; set; }
    public string KeyId { get; set; } = default!;
    public string PublicKey { get; set; } = default!;
}

/// <summary>Outcome of a membership or settings change, for the real-time notifications.</summary>
public class GroupChangeResult
{
    public Guid GroupId { get; set; }

    /// <summary>Members after the change.</summary>
    public List<Guid> MemberIds { get; set; } = new();

    /// <summary>Users who are no longer members (removed, left, or the group was deleted).</summary>
    public List<Guid> RemovedUserIds { get; set; } = new();

    /// <summary>The service message shown in the chat, if any.</summary>
    public global::Message? SystemMessage { get; set; }
}
