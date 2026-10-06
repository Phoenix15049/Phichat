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

        return new GroupDetailsDto
        {
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
        await EnsureAdminAsync(userId, groupId);
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
        await EnsureAdminAsync(userId, groupId);

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

    public async Task<GroupChangeResult> AddMembersAsync(Guid userId, Guid groupId, IReadOnlyCollection<Guid> userIds)
    {
        await EnsureAdminAsync(userId, groupId);

        var current = await GetMemberIdsAsync(groupId);
        var added = userIds.Distinct().Where(id => !current.Contains(id)).ToList();
        if (added.Count == 0) return await ResultAsync(groupId, null);
        if (current.Count + added.Count > ValidationRules.GroupMaxMembers) throw TooManyMembers();
        await EnsureCanAddAsync(userId, added);

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
        await _db.PinnedMessages.Where(p => messages.Any(m => m.Id == p.MessageId)).ExecuteDeleteAsync();
        await _db.MessageReactions.Where(r => messages.Any(m => m.Id == r.MessageId)).ExecuteDeleteAsync();
        await _db.MessageHides.Where(h => messages.Any(m => m.Id == h.MessageId)).ExecuteDeleteAsync();
        // Replies point at messages of the same group; unlink them so the delete never trips the reply key.
        await messages.Where(m => m.ReplyToMessageId != null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReplyToMessageId, (Guid?)null));
        await messages.ExecuteDeleteAsync();
        await _db.ChatMutes.Where(m => m.ChatId == groupId).ExecuteDeleteAsync();
        await _db.GroupMembers.Where(gm => gm.GroupId == groupId).ExecuteDeleteAsync();
        await _db.Groups.Where(g => g.Id == groupId).ExecuteDeleteAsync();
        await tx.CommitAsync();

        return new GroupChangeResult { GroupId = groupId, RemovedUserIds = members };
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
