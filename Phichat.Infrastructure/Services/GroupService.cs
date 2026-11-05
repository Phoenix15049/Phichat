using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Group;
using Phichat.Application.Interfaces;
using Phichat.Application.Validators;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;
using Group = Phichat.Domain.Entities.Group;

namespace Phichat.Infrastructure.Services;

public sealed class GroupService : IGroupService
{
    private static readonly JsonSerializerOptions EventJson = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly IBlockService _blocks;

    public GroupService(AppDbContext db, IBlockService blocks)
    {
        _db = db;
        _blocks = blocks;
    }

    public async Task<GroupChangeResult> CreateAsync(Guid creatorId, CreateGroupRequest request)
    {
        var title = CleanTitle(request.Title);
        var memberIds = (request.MemberIds ?? new List<Guid>()).Where(id => id != creatorId).Distinct().ToList();
        if (memberIds.Count + 1 > ValidationRules.GroupMaxMembers) throw TooManyMembers();
        await EnsureCanAddAsync(creatorId, memberIds);

        var now = DateTime.UtcNow;
        var group = new Group { Id = Guid.NewGuid(), Title = title, CreatedById = creatorId, CreatedAtUtc = now };
        _db.Groups.Add(group);
        _db.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = creatorId, Role = GroupRole.Owner, JoinedAtUtc = now });
        foreach (var id in memberIds)
            _db.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = id, Role = GroupRole.Member, JoinedAtUtc = now });

        var message = SystemMessage(group.Id, creatorId, now, new { type = "created", title });
        await _db.SaveChangesAsync();

        return new GroupChangeResult
        {
            GroupId = group.Id,
            MemberIds = memberIds.Prepend(creatorId).ToList(),
            SystemMessage = message
        };
    }

    public async Task<GroupDetailsDto> GetAsync(Guid userId, Guid groupId)
    {
        var me = await RequireMemberAsync(userId, groupId);
        var group = await _db.Groups.AsNoTracking().FirstAsync(g => g.Id == groupId);

        var members = await _db.GroupMembers.AsNoTracking()
            .Where(gm => gm.GroupId == groupId)
            .OrderByDescending(gm => gm.Role).ThenBy(gm => gm.JoinedAtUtc)
            .Select(gm => new
            {
                gm.UserId,
                gm.User.Username,
                gm.User.DisplayName,
                gm.User.AvatarUrl,
                gm.Role,
                gm.JoinedAtUtc
            })
            .ToListAsync();

        var mayInvite = me.Role != GroupRole.Member || group.MemberPermissions.HasFlag(GroupPermissions.AddMembers);

        return new GroupDetailsDto
        {
            Permissions = ToDto(group.MemberPermissions),
            SlowModeSeconds = group.SlowModeSeconds,
            InviteCode = mayInvite ? group.InviteCode : null,
            BannedCount = me.Role == GroupRole.Member ? 0 : await _db.GroupBans.CountAsync(b => b.GroupId == groupId),
            Id = group.Id,
            Title = group.Title,
            Description = group.Description,
            AvatarUrl = group.AvatarUrl,
            CreatedAtUtc = group.CreatedAtUtc,
            MyRole = RoleName(me.Role),
            Members = members.Select(m => new GroupMemberDto
            {
                UserId = m.UserId,
                Username = m.Username,
                DisplayName = m.DisplayName,
                AvatarUrl = m.AvatarUrl,
                Role = RoleName(m.Role),
                JoinedAtUtc = m.JoinedAtUtc
            }).ToList()
        };
    }

    public async Task<GroupChangeResult> UpdateAsync(Guid userId, Guid groupId, UpdateGroupRequest request)
    {
        await EnsureCanChangeInfoAsync(userId, groupId);
        var group = await _db.Groups.FirstAsync(g => g.Id == groupId);

        var title = CleanTitle(request.Title);
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (description?.Length > ValidationRules.GroupDescriptionMaxLength)
            throw new BadRequestException("invalid_group_description", "The description is too long.");

        Message? message = null;
        if (title != group.Title)
            message = SystemMessage(groupId, userId, DateTime.UtcNow, new { type = "title", title });

        group.Title = title;
        group.Description = description;
        await _db.SaveChangesAsync();

        return await ResultAsync(groupId, message);
    }

    public async Task<GroupChangeResult> SetAvatarAsync(Guid userId, Guid groupId, string? avatarUrl)
    {
        await EnsureCanChangeInfoAsync(userId, groupId);

        if (avatarUrl != null && !Regex.IsMatch(avatarUrl, $@"^/uploads/groups/{groupId}/[0-9]{{17}}\.(jpe?g|png|webp|gif)$", RegexOptions.IgnoreCase))
            throw new BadRequestException("invalid_avatar_url", "The group photo must be uploaded to this group.");

        var group = await _db.Groups.FirstAsync(g => g.Id == groupId);
        group.AvatarUrl = avatarUrl;
        var message = SystemMessage(groupId, userId, DateTime.UtcNow, new { type = avatarUrl == null ? "photoRemoved" : "photo" });
        await _db.SaveChangesAsync();

        return await ResultAsync(groupId, message);
    }

    public async Task EnsureAdminAsync(Guid userId, Guid groupId)
    {
        var me = await RequireMemberAsync(userId, groupId);
        if (me.Role == GroupRole.Member)
            throw new ForbiddenException("not_group_admin", "Only group admins can do this.");
    }

    public async Task EnsureCanChangeInfoAsync(Guid userId, Guid groupId) =>
        await EnsureAllowedAsync(userId, groupId, GroupPermissions.ChangeInfo);

    public async Task<GroupChangeResult> UpdateSettingsAsync(Guid userId, Guid groupId, UpdateGroupSettingsRequest request)
    {
        await EnsureAdminAsync(userId, groupId);

        if (request.SlowModeSeconds < 0 || request.SlowModeSeconds > ValidationRules.GroupMaxSlowModeSeconds)
            throw new BadRequestException("invalid_slow_mode", "The slow mode delay is out of range.");

        var group = await _db.Groups.FirstAsync(g => g.Id == groupId);
        var permissions = FromDto(request.Permissions);

        Message? message = null;
        if (group.SlowModeSeconds != request.SlowModeSeconds)
            message = SystemMessage(groupId, userId, DateTime.UtcNow, new { type = "slowMode", seconds = request.SlowModeSeconds });
        else if (group.MemberPermissions != permissions)
            message = SystemMessage(groupId, userId, DateTime.UtcNow, new { type = "permissions" });

        group.MemberPermissions = permissions;
        group.SlowModeSeconds = request.SlowModeSeconds;
        await _db.SaveChangesAsync();

        return await ResultAsync(groupId, message);
    }

    public async Task<GroupChangeResult> TransferOwnershipAsync(Guid userId, Guid groupId, Guid newOwnerId)
    {
        var me = await _db.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId)
            ?? throw GroupNotFound();
        if (me.Role != GroupRole.Owner)
            throw new ForbiddenException("not_group_owner", "Only the group owner can hand the group over.");
        if (newOwnerId == userId) return await ResultAsync(groupId, null);

        var target = await _db.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == newOwnerId)
            ?? throw new NotFoundException("not_group_member", "This user is not in the group.");

        me.Role = GroupRole.Admin;
        target.Role = GroupRole.Owner;
        var message = SystemMessage(groupId, userId, DateTime.UtcNow, new { type = "owner", users = new[] { newOwnerId } });
        await _db.SaveChangesAsync();

        return await ResultAsync(groupId, message);
    }

    public async Task<GroupChangeResult> ResetInviteAsync(Guid userId, Guid groupId)
    {
        await EnsureAdminAsync(userId, groupId);

        var group = await _db.Groups.FirstAsync(g => g.Id == groupId);
        group.InviteCode = NewInviteCode();
        await _db.SaveChangesAsync();

        return await ResultAsync(groupId, null);
    }

    public async Task<GroupChangeResult> RevokeInviteAsync(Guid userId, Guid groupId)
    {
        await EnsureAdminAsync(userId, groupId);

        var group = await _db.Groups.FirstAsync(g => g.Id == groupId);
        group.InviteCode = null;
        await _db.SaveChangesAsync();

        return await ResultAsync(groupId, null);
    }

    public async Task<GroupInvitePreviewDto> GetInvitePreviewAsync(Guid userId, string code)
    {
        var group = await GroupOfInviteAsync(code);

        return new GroupInvitePreviewDto
        {
            GroupId = group.Id,
            Title = group.Title,
            Description = group.Description,
            AvatarUrl = group.AvatarUrl,
            MemberCount = await _db.GroupMembers.CountAsync(gm => gm.GroupId == group.Id),
            IsMember = await IsMemberAsync(userId, group.Id)
        };
    }

    public async Task<GroupChangeResult> JoinByInviteAsync(Guid userId, string code)
    {
        var group = await GroupOfInviteAsync(code);

        var current = await GetMemberIdsAsync(group.Id);
        if (current.Contains(userId)) return await ResultAsync(group.Id, null);
        if (current.Count + 1 > ValidationRules.GroupMaxMembers) throw TooManyMembers();

        if (await _db.GroupBans.AnyAsync(b => b.GroupId == group.Id && b.UserId == userId))
            throw new ForbiddenException("group_banned", "You were removed from this group.");

        var now = DateTime.UtcNow;
        _db.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = userId, Role = GroupRole.Member, JoinedAtUtc = now });
        var message = SystemMessage(group.Id, userId, now, new { type = "joined" });
        await _db.SaveChangesAsync();

        return await ResultAsync(group.Id, message);
    }

    public async Task<List<GroupBanDto>> GetBansAsync(Guid userId, Guid groupId)
    {
        await EnsureAdminAsync(userId, groupId);

        return await _db.GroupBans.AsNoTracking()
            .Where(b => b.GroupId == groupId)
            .OrderByDescending(b => b.BannedAtUtc)
            .Select(b => new GroupBanDto
            {
                UserId = b.UserId,
                Username = b.User.Username,
                DisplayName = b.User.DisplayName,
                AvatarUrl = b.User.AvatarUrl,
                BannedAtUtc = b.BannedAtUtc
            })
            .ToListAsync();
    }

    public async Task<GroupChangeResult> UnbanAsync(Guid userId, Guid groupId, Guid bannedId)
    {
        await EnsureAdminAsync(userId, groupId);
        await _db.GroupBans.Where(b => b.GroupId == groupId && b.UserId == bannedId).ExecuteDeleteAsync();
        return await ResultAsync(groupId, null);
    }

    public async Task<List<CommonGroupDto>> GetCommonAsync(Guid userId, Guid otherId)
    {
        var mine = _db.GroupMembers.Where(gm => gm.UserId == userId).Select(gm => gm.GroupId);

        return await _db.GroupMembers.AsNoTracking()
            .Where(gm => gm.UserId == otherId && mine.Contains(gm.GroupId))
            .OrderBy(gm => gm.Group.Title)
            .Select(gm => new CommonGroupDto
            {
                Id = gm.GroupId,
                Title = gm.Group.Title,
                AvatarUrl = gm.Group.AvatarUrl,
                MemberCount = _db.GroupMembers.Count(x => x.GroupId == gm.GroupId)
            })
            .ToListAsync();
    }

    public async Task<GroupChangeResult> AddMembersAsync(Guid userId, Guid groupId, IReadOnlyCollection<Guid> userIds)
    {
        var me = await EnsureAllowedAsync(userId, groupId, GroupPermissions.AddMembers);

        var current = await GetMemberIdsAsync(groupId);
        var added = userIds.Distinct().Where(id => !current.Contains(id)).ToList();
        if (added.Count == 0) return await ResultAsync(groupId, null);
        if (current.Count + added.Count > ValidationRules.GroupMaxMembers) throw TooManyMembers();
        await EnsureCanAddAsync(userId, added);

        // Bringing back someone who was removed is the admins' decision, and clears the removal.
        var banned = await _db.GroupBans.Where(b => b.GroupId == groupId && added.Contains(b.UserId)).ToListAsync();
        if (banned.Count > 0 && me.Role == GroupRole.Member)
            throw new ForbiddenException("cannot_add_user", "This user cannot be added to the group.");
        _db.GroupBans.RemoveRange(banned);

        var now = DateTime.UtcNow;
        foreach (var id in added)
            _db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = id, Role = GroupRole.Member, JoinedAtUtc = now });

        // Sent at the join time, so the new members see it as their first message.
        var message = SystemMessage(groupId, userId, now, new { type = "added", users = added });
        await _db.SaveChangesAsync();

        return await ResultAsync(groupId, message);
    }

    public async Task<GroupChangeResult> RemoveMemberAsync(Guid userId, Guid groupId, Guid memberId)
    {
        if (memberId == userId) return await LeaveAsync(userId, groupId);

        var me = await RequireMemberAsync(userId, groupId);
        var target = await _db.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == memberId)
            ?? throw new NotFoundException("not_group_member", "This user is not in the group.");

        // Admins remove members; only the owner removes admins; nobody removes the owner.
        var allowed = me.Role == GroupRole.Owner || (me.Role == GroupRole.Admin && target.Role == GroupRole.Member);
        if (!allowed) throw new ForbiddenException("not_group_admin", "You cannot remove this member.");

        _db.GroupMembers.Remove(target);
        _db.GroupBans.Add(new GroupBan { GroupId = groupId, UserId = memberId, BannedById = userId, BannedAtUtc = DateTime.UtcNow });
        var message = SystemMessage(groupId, userId, DateTime.UtcNow, new { type = "removed", users = new[] { memberId } });
        await _db.SaveChangesAsync();
        await _db.ChatMutes.Where(m => m.UserId == memberId && m.ChatId == groupId).ExecuteDeleteAsync();

        var result = await ResultAsync(groupId, message);
        result.RemovedUserIds.Add(memberId);
        return result;
    }

    public async Task<GroupChangeResult> LeaveAsync(Guid userId, Guid groupId)
    {
        var me = await _db.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId)
            ?? throw GroupNotFound();

        var others = await _db.GroupMembers
            .Where(gm => gm.GroupId == groupId && gm.UserId != userId)
            .OrderByDescending(gm => gm.Role).ThenBy(gm => gm.JoinedAtUtc)
            .ToListAsync();

        if (others.Count == 0)
        {
            var deleted = await DeleteGroupAsync(groupId);
            return deleted;
        }

        // The group always keeps an owner: the longest-serving admin, otherwise the longest member.
        if (me.Role == GroupRole.Owner)
            others[0].Role = GroupRole.Owner;

        _db.GroupMembers.Remove(me);
        var message = SystemMessage(groupId, userId, DateTime.UtcNow, new { type = "left" });
        await _db.SaveChangesAsync();
        await _db.ChatMutes.Where(m => m.UserId == userId && m.ChatId == groupId).ExecuteDeleteAsync();

        var result = await ResultAsync(groupId, message);
        result.RemovedUserIds.Add(userId);
        return result;
    }

    public async Task<GroupChangeResult> SetRoleAsync(Guid userId, Guid groupId, Guid memberId, string role)
    {
        var me = await RequireMemberAsync(userId, groupId);
        if (me.Role != GroupRole.Owner)
            throw new ForbiddenException("not_group_owner", "Only the group owner can change admins.");

        var target = await _db.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == memberId)
            ?? throw new NotFoundException("not_group_member", "This user is not in the group.");
        if (target.Role == GroupRole.Owner)
            throw new BadRequestException("cannot_change_owner", "The owner's role cannot be changed.");

        target.Role = role switch
        {
            "admin" => GroupRole.Admin,
            "member" => GroupRole.Member,
            _ => throw new BadRequestException("invalid_role", "Role must be 'admin' or 'member'.")
        };
        await _db.SaveChangesAsync();

        return await ResultAsync(groupId, null);
    }

    public async Task<GroupChangeResult> DeleteAsync(Guid userId, Guid groupId)
    {
        var me = await RequireMemberAsync(userId, groupId);
        if (me.Role != GroupRole.Owner)
            throw new ForbiddenException("not_group_owner", "Only the group owner can delete the group.");

        return await DeleteGroupAsync(groupId);
    }

    public Task<List<Guid>> GetMemberIdsAsync(Guid groupId) =>
        _db.GroupMembers.Where(gm => gm.GroupId == groupId).Select(gm => gm.UserId).ToListAsync();

    public Task<bool> IsMemberAsync(Guid userId, Guid groupId) =>
        _db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

    public async Task<List<GroupMemberKeyDto>> GetMemberKeysAsync(Guid userId, Guid groupId)
    {
        await RequireMemberAsync(userId, groupId);

        return await _db.UserIdentityKeys.AsNoTracking()
            .Where(k => k.RevokedAtUtc == null && _db.GroupMembers.Any(gm => gm.GroupId == groupId && gm.UserId == k.UserId))
            .Select(k => new GroupMemberKeyDto { UserId = k.UserId, KeyId = k.KeyId, PublicKey = k.PublicKey })
            .ToListAsync();
    }

    // ---- helpers ----

    private async Task<GroupChangeResult> DeleteGroupAsync(Guid groupId)
    {
        var members = await GetMemberIdsAsync(groupId);

        await using var tx = await _db.Database.BeginTransactionAsync();
        var messages = _db.Messages.Where(m => m.GroupId == groupId);
        var fileUrls = await messages.Where(m => m.FileUrl != null).Select(m => m.FileUrl!).Distinct().ToListAsync();
        await _db.PinnedMessages.Where(p => messages.Any(m => m.Id == p.MessageId)).ExecuteDeleteAsync();
        await _db.MessageReactions.Where(r => messages.Any(m => m.Id == r.MessageId)).ExecuteDeleteAsync();
        await _db.MessageHides.Where(h => messages.Any(m => m.Id == h.MessageId)).ExecuteDeleteAsync();
        // Replies point at messages of the same group; unlink them so the delete never trips the reply key.
        await messages.Where(m => m.ReplyToMessageId != null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReplyToMessageId, (Guid?)null));
        await messages.ExecuteDeleteAsync();
        await _db.ChatMutes.Where(m => m.ChatId == groupId).ExecuteDeleteAsync();
        await _db.GroupBans.Where(b => b.GroupId == groupId).ExecuteDeleteAsync();
        await _db.GroupMembers.Where(gm => gm.GroupId == groupId).ExecuteDeleteAsync();
        await _db.Groups.Where(g => g.Id == groupId).ExecuteDeleteAsync();
        await tx.CommitAsync();

        // A file forwarded to another chat stays.
        var stillUsed = await _db.Messages.Where(m => m.FileUrl != null && fileUrls.Contains(m.FileUrl)).Select(m => m.FileUrl!).Distinct().ToListAsync();

        return new GroupChangeResult { GroupId = groupId, RemovedUserIds = members, OrphanedFileUrls = fileUrls.Except(stillUsed).ToList() };
    }

    private async Task<GroupChangeResult> ResultAsync(Guid groupId, Message? message) => new()
    {
        GroupId = groupId,
        MemberIds = await GetMemberIdsAsync(groupId),
        SystemMessage = message
    };

    /// <summary>A service message ("X added Y"); the server writes it, so it is not encrypted.</summary>
    private Message SystemMessage(Guid groupId, Guid actorId, DateTime at, object data)
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderId = actorId,
            GroupId = groupId,
            EncryptedContent = "",
            SystemEvent = JsonSerializer.Serialize(data, EventJson),
            SentAt = at,
            DeliveredAtUtc = at,
            IsRead = true
        };
        _db.Messages.Add(message);
        return message;
    }

    /// <summary>Admins may do everything; a member only what the group allows.</summary>
    private async Task<GroupMember> EnsureAllowedAsync(Guid userId, Guid groupId, GroupPermissions permission)
    {
        var me = await RequireMemberAsync(userId, groupId);
        if (me.Role != GroupRole.Member) return me;

        var allowed = await _db.Groups.Where(g => g.Id == groupId).Select(g => g.MemberPermissions).FirstAsync();
        if (!allowed.HasFlag(permission))
            throw new ForbiddenException("not_group_admin", "Only group admins can do this.");
        return me;
    }

    private async Task<Group> GroupOfInviteAsync(string code) =>
        await _db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.InviteCode == code)
        ?? throw new NotFoundException("invite_not_found", "This invite link is not valid any more.");

    /// <summary>128 random bits in URL-safe text: a link cannot be guessed.</summary>
    private static string NewInviteCode() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static GroupPermissionsDto ToDto(GroupPermissions value) => new()
    {
        SendMessages = value.HasFlag(GroupPermissions.SendMessages),
        SendMedia = value.HasFlag(GroupPermissions.SendMedia),
        PinMessages = value.HasFlag(GroupPermissions.PinMessages),
        AddMembers = value.HasFlag(GroupPermissions.AddMembers),
        ChangeInfo = value.HasFlag(GroupPermissions.ChangeInfo)
    };

    private static GroupPermissions FromDto(GroupPermissionsDto? value)
    {
        if (value == null) return GroupPermissions.Default;

        var result = GroupPermissions.None;
        if (value.SendMessages) result |= GroupPermissions.SendMessages;
        // Sending media is a kind of sending: it cannot stay on when messages are off.
        if (value.SendMessages && value.SendMedia) result |= GroupPermissions.SendMedia;
        if (value.PinMessages) result |= GroupPermissions.PinMessages;
        if (value.AddMembers) result |= GroupPermissions.AddMembers;
        if (value.ChangeInfo) result |= GroupPermissions.ChangeInfo;
        return result;
    }

    private async Task<GroupMember> RequireMemberAsync(Guid userId, Guid groupId) =>
        await _db.GroupMembers.AsNoTracking().FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId)
        ?? throw GroupNotFound();

    /// <summary>Existing users who have not blocked the one adding them (and are not blocked by them).</summary>
    private async Task EnsureCanAddAsync(Guid actorId, IReadOnlyCollection<Guid> userIds)
    {
        if (userIds.Count == 0) return;

        var existing = await _db.Users.CountAsync(u => userIds.Contains(u.Id));
        if (existing != userIds.Count)
            throw new NotFoundException("user_not_found", "User not found.");

        foreach (var id in userIds)
        {
            if (await _blocks.IsBlockedEitherWayAsync(actorId, id))
                throw new ForbiddenException("cannot_add_user", "This user cannot be added to the group.");
        }
    }

    private static string CleanTitle(string? title)
    {
        var trimmed = title?.Trim() ?? "";
        if (trimmed.Length == 0 || trimmed.Length > ValidationRules.GroupTitleMaxLength)
            throw new BadRequestException("invalid_group_title", "The group name is required and must be short.");
        return trimmed;
    }

    private static string RoleName(GroupRole role) => role switch
    {
        GroupRole.Owner => "owner",
        GroupRole.Admin => "admin",
        _ => "member"
    };

    private static BadRequestException TooManyMembers() =>
        new("too_many_members", $"A group can have at most {ValidationRules.GroupMaxMembers} members.");

    private static NotFoundException GroupNotFound() => new("group_not_found", "Group not found.");
}
