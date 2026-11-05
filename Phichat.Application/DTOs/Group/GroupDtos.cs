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

/// <summary>What ordinary members of a group may do.</summary>
public class GroupPermissionsDto
{
    public bool SendMessages { get; set; }
    public bool SendMedia { get; set; }
    public bool PinMessages { get; set; }
    public bool AddMembers { get; set; }
    public bool ChangeInfo { get; set; }
}

public class UpdateGroupSettingsRequest
{
    public GroupPermissionsDto Permissions { get; set; } = new();
    public int SlowModeSeconds { get; set; }
}

public class TransferGroupRequest
{
    public Guid UserId { get; set; }
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
    public GroupPermissionsDto Permissions { get; set; } = new();
    public int SlowModeSeconds { get; set; }

    /// <summary>Only for those who may invite people.</summary>
    public string? InviteCode { get; set; }
    public int BannedCount { get; set; }
}

/// <summary>What someone holding an invite link sees before joining.</summary>
public class GroupInvitePreviewDto
{
    public Guid GroupId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public int MemberCount { get; set; }
    public bool IsMember { get; set; }
}

public class GroupBanDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = default!;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime BannedAtUtc { get; set; }
}

/// <summary>A group two people share.</summary>
public class CommonGroupDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public int MemberCount { get; set; }
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

    /// <summary>Attachments no message uses any more (the group was deleted), to remove from storage.</summary>
    public List<string> OrphanedFileUrls { get; set; } = new();
}
