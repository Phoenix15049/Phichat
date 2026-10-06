using Phichat.Application.DTOs.Group;

namespace Phichat.Application.Interfaces;

/// <summary>
/// Groups and their members. Roles: the owner (one), admins (manage the group and its members)
/// and members. Changes are recorded as service messages in the chat.
/// </summary>
public interface IGroupService
{
    Task<GroupChangeResult> CreateAsync(Guid creatorId, CreateGroupRequest request);

    /// <summary>The group with its members; only for members.</summary>
    Task<GroupDetailsDto> GetAsync(Guid userId, Guid groupId);

    Task<GroupChangeResult> UpdateAsync(Guid userId, Guid groupId, UpdateGroupRequest request);

    /// <summary>Sets (or with null removes) the photo, given as a path under /uploads/groups/{groupId}/.</summary>
    Task<GroupChangeResult> SetAvatarAsync(Guid userId, Guid groupId, string? avatarUrl);

    /// <summary>Throws unless the user is an admin or the owner of the group.</summary>
    Task EnsureAdminAsync(Guid userId, Guid groupId);

    Task<GroupChangeResult> AddMembersAsync(Guid userId, Guid groupId, IReadOnlyCollection<Guid> userIds);

    Task<GroupChangeResult> RemoveMemberAsync(Guid userId, Guid groupId, Guid memberId);

    /// <summary>Leaves the group. An owner hands the group to an admin (or the longest member); the last member deletes it.</summary>
    Task<GroupChangeResult> LeaveAsync(Guid userId, Guid groupId);

    /// <summary>Owner only: makes a member an admin or back.</summary>
    Task<GroupChangeResult> SetRoleAsync(Guid userId, Guid groupId, Guid memberId, string role);

    /// <summary>Owner only: deletes the group and its messages for everyone.</summary>
    Task<GroupChangeResult> DeleteAsync(Guid userId, Guid groupId);

    Task<List<Guid>> GetMemberIdsAsync(Guid groupId);

    Task<bool> IsMemberAsync(Guid userId, Guid groupId);

    /// <summary>Current identity keys of the members, for encrypting a message to the group.</summary>
    Task<List<GroupMemberKeyDto>> GetMemberKeysAsync(Guid userId, Guid groupId);
}
