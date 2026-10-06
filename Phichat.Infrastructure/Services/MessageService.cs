using Microsoft.EntityFrameworkCore;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Interfaces;
using Phichat.Application.Validators;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;
using Phichat.Infrastructure.Files;

public class MessageService : IMessageService
{
    private readonly AppDbContext _context;
    private readonly IIdentityKeyService _identityKeys;
    private readonly IBlockService _blocks;
    private readonly IPrivacyService _privacy;

    public MessageService(AppDbContext context, IIdentityKeyService identityKeys, IBlockService blocks, IPrivacyService privacy)
    {
        _context = context;
        _identityKeys = identityKeys;
        _blocks = blocks;
        _privacy = privacy;
    }

    /// <summary>The chat a new message goes to: a private chat with <c>ReceiverId</c>, or a group.</summary>
    private sealed record ChatTarget(Guid? ReceiverId, Guid? GroupId);

    public async Task<Message> SendMessageAsync(Guid senderId, SendMessageRequest request)
    {
        // Hub calls bypass MVC model validation, so the body limits are enforced here too.
        var target = await ResolveTargetAsync(senderId, request.ReceiverId, request.GroupId, request.EncryptedText);
        await EnsureReplyTargetAsync(senderId, target, request.ReplyToMessageId);

        var message = NewMessage(senderId, target, request.EncryptedText, request.ReplyToMessageId);

        if (request.ForwardedFromMessageId.HasValue)
        {
            // The client re-encrypts the text for the new chat; the attachment is copied server-side.
            var source = await ResolveForwardSourceAsync(senderId, request.ForwardedFromMessageId.Value);
            message.ForwardedFromMessageId = source.MessageId;
            message.ForwardedFromSenderId = source.OriginalSenderId;
            message.FileUrl = source.FileUrl;
        }

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }

    public async Task<Message> SendMessageWithFileAsync(Guid senderId, SendMessageWithFileRequest request, string uploadRootPath)
    {
        // The file key travels inside the encrypted text, so the text is always required here.
        var target = await ResolveTargetAsync(senderId, request.ReceiverId, request.GroupId, request.EncryptedText);
        await EnsureReplyTargetAsync(senderId, target, request.ReplyToMessageId);

        if (request.File == null || request.File.Length == 0)
            throw new BadRequestException("file_required", "File is required.");

        // Random prefix + sanitized name: the client name is kept for display but can never escape the folder.
        var storedName = $"{Guid.NewGuid():N}_{FileNameSanitizer.Sanitize(request.File.FileName)}";
        var fullPath = Path.Combine(uploadRootPath, storedName);

        await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
        {
            await request.File.CopyToAsync(stream);
        }

        var message = NewMessage(senderId, target, request.EncryptedText, request.ReplyToMessageId);
        message.FileUrl = "/uploads/" + Uri.EscapeDataString(storedName);

        if (request.ForwardedFromMessageId.HasValue)
        {
            var source = await ResolveForwardSourceAsync(senderId, request.ForwardedFromMessageId.Value);
            message.ForwardedFromMessageId = source.MessageId;
            message.ForwardedFromSenderId = source.OriginalSenderId;
        }

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }

    private static Message NewMessage(Guid senderId, ChatTarget target, string encryptedText, Guid? replyToMessageId)
    {
        var now = DateTime.UtcNow;
        return new Message
        {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            ReceiverId = target.ReceiverId,
            GroupId = target.GroupId,
            EncryptedContent = encryptedText,
            SentAt = now,
            DeliveredAtUtc = now,
            ReplyToMessageId = replyToMessageId
        };
    }

    /// <summary>Checks that the sender may write to the chat and that the body is encrypted for its members.</summary>
    private async Task<ChatTarget> ResolveTargetAsync(Guid senderId, Guid receiverId, Guid? groupId, string? encryptedText)
    {
        if (groupId.HasValue)
        {
            if (receiverId != Guid.Empty)
                throw new BadRequestException("invalid_target", "Set either a receiver or a group.");
            if (string.IsNullOrEmpty(encryptedText) || encryptedText.Length > ValidationRules.GroupEncryptedTextMaxLength)
                throw new BadRequestException("invalid_message", "Message is empty or too large.");

            await RequireMembershipAsync(groupId.Value, senderId);
            await _identityKeys.EnsureGroupMessageKeysAsync(senderId, await MemberIdsAsync(groupId.Value), encryptedText);
            return new ChatTarget(null, groupId);
        }

        if (string.IsNullOrEmpty(encryptedText) || encryptedText.Length > ValidationRules.EncryptedTextMaxLength)
            throw new BadRequestException("invalid_message", "Message is empty or too large.");

        await EnsureReceiverExistsAsync(receiverId);
        await _blocks.EnsureCanMessageAsync(senderId, receiverId);
        await _identityKeys.EnsureMessageKeysAsync(senderId, receiverId, encryptedText);
        return new ChatTarget(receiverId, null);
    }

    public async Task<MessageReadResult> MarkAsReadAsync(Guid messageId, Guid readerId)
    {
        var message = await _context.Messages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ReceiverId == readerId);

        if (message == null || message.IsRead)
            return new MessageReadResult { Success = false };

        message.IsRead = true;
        message.ReadAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new MessageReadResult
        {
            Success = true,
            SenderId = message.SenderId,
            MessageId = message.Id,
            ReadAtUtc = message.ReadAtUtc
        };
    }

    public async Task<DateTime?> MarkGroupReadAsync(Guid groupId, Guid readerId, Guid messageId)
    {
        var member = await _context.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == readerId)
            ?? throw GroupNotFound();

        var sentAt = await _context.Messages
            .Where(m => m.Id == messageId && m.GroupId == groupId && m.SentAt >= member.JoinedAtUtc)
            .Select(m => (DateTime?)m.SentAt)
            .FirstOrDefaultAsync();

        // Reading is monotonic: an older message does not move the position back.
        if (sentAt == null || member.LastReadAtUtc >= sentAt) return null;

        member.LastReadAtUtc = sentAt;
        await _context.SaveChangesAsync();
        return sentAt;
    }

    public async Task<List<ConversationDto>> GetConversationsAsync(Guid currentUserId)
    {
        // Only messages the user can still see: not deleted for everyone, not hidden ("delete for me").
        var hiddenIds = _context.MessageHides
            .Where(h => h.UserId == currentUserId)
            .Select(h => h.MessageId);

        var baseQuery = _context.Messages
            .Where(m => m.GroupId == null && (m.SenderId == currentUserId || m.ReceiverId == currentUserId))
            .Where(m => !m.IsDeleted && !hiddenIds.Contains(m.Id))
            .Select(m => new
            {
                PeerId = m.SenderId == currentUserId ? m.ReceiverId!.Value : m.SenderId,
                Msg = m
            });
        var grouped = await baseQuery
            .GroupBy(x => x.PeerId)
            .Select(g => new
            {
                PeerId = g.Key,
                Last = g.OrderByDescending(x => x.Msg.SentAt).FirstOrDefault()!.Msg,
                Unread = g.Count(x => x.Msg.ReceiverId == currentUserId && !x.Msg.IsRead)
            })
            .ToListAsync();

        var peerIds = grouped.Select(x => x.PeerId).ToList();
        var peers = await _context.Users
            .Where(u => peerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, u.DisplayName, u.AvatarUrl })
            .ToListAsync();

        var result = grouped
            .Select(x =>
            {
                var p = peers.FirstOrDefault(u => u.Id == x.PeerId);
                return new ConversationDto
                {
                    PeerId = x.PeerId,
                    PeerUsername = p?.Username ?? "unknown",
                    PeerDisplayName = p?.DisplayName,
                    PeerAvatarUrl = p?.AvatarUrl,
                    LastSenderId = x.Last.SenderId,
                    LastEncryptedContent = x.Last.EncryptedContent,
                    LastFileUrl = x.Last.FileUrl,
                    LastSentAt = x.Last.SentAt,
                    UnreadCount = x.Unread
                };
            })
            .ToList();

        result.AddRange(await GetGroupConversationsAsync(currentUserId));
        return result.OrderByDescending(c => c.LastSentAt).ToList();
    }

    private async Task<List<ConversationDto>> GetGroupConversationsAsync(Guid me)
    {
        var memberships = await _context.GroupMembers.AsNoTracking()
            .Where(gm => gm.UserId == me)
            .Select(gm => new
            {
                gm.GroupId,
                gm.JoinedAtUtc,
                gm.LastReadAtUtc,
                gm.Group.Title,
                gm.Group.AvatarUrl,
                MemberCount = _context.GroupMembers.Count(x => x.GroupId == gm.GroupId)
            })
            .ToListAsync();

        var result = new List<ConversationDto>();
        foreach (var g in memberships)
        {
            var visible = VisibleTo(me).Where(m => m.GroupId == g.GroupId && !m.IsDeleted);
            var last = await visible.OrderByDescending(m => m.SentAt).FirstOrDefaultAsync();
            var readFrom = g.LastReadAtUtc ?? g.JoinedAtUtc;
            var unread = await visible.CountAsync(m => m.SenderId != me && m.SystemEvent == null && m.SentAt > readFrom);

            result.Add(new ConversationDto
            {
                PeerId = g.GroupId,
                IsGroup = true,
                MemberCount = g.MemberCount,
                PeerUsername = "",
                PeerDisplayName = g.Title,
                PeerAvatarUrl = g.AvatarUrl,
                LastSenderId = last?.SenderId ?? Guid.Empty,
                LastEncryptedContent = last?.EncryptedContent,
                LastFileUrl = last?.FileUrl,
                LastSystemEvent = last?.SystemEvent,
                LastSentAt = last?.SentAt ?? g.JoinedAtUtc,
                UnreadCount = unread
            });
        }
        return result;
    }

    public async Task<PagedMessagesResponse> GetConversationPageAsync(Guid me, Guid other, Guid? beforeId, int pageSize)
    {
        var q = _context.Messages.AsNoTracking()
            .Where(m => (m.SenderId == me && m.ReceiverId == other) || (m.SenderId == other && m.ReceiverId == me));

        var page = await LoadPageAsync(q, me, beforeId, pageSize);

        // Read state of my messages is shown only while both of us share read receipts.
        if (page.Items.Any(i => i.SenderId == me && i.IsRead) && !await _privacy.ReadReceiptsSharedAsync(me, other))
        {
            foreach (var item in page.Items.Where(i => i.SenderId == me))
            {
                item.IsRead = false;
                item.ReadAtUtc = null;
            }
        }

        return page;
    }

    public async Task<PagedMessagesResponse> GetGroupPageAsync(Guid me, Guid groupId, Guid? beforeId, int pageSize)
    {
        var member = await RequireMembershipAsync(groupId, me);

        // Members only see what was sent after they joined (they could not decrypt earlier messages anyway).
        var q = _context.Messages.AsNoTracking()
            .Where(m => m.GroupId == groupId && m.SentAt >= member.JoinedAtUtc);

        var page = await LoadPageAsync(q, me, beforeId, pageSize);

        // A message counts as read once any other member has read it; mine are read up to my own position.
        var othersReadUpTo = await _context.GroupMembers.AsNoTracking()
            .Where(gm => gm.GroupId == groupId && gm.UserId != me)
            .MaxAsync(gm => gm.LastReadAtUtc);
        foreach (var item in page.Items)
        {
            item.IsRead = item.SenderId == me
                ? othersReadUpTo.HasValue && item.SentAt <= othersReadUpTo.Value
                : member.LastReadAtUtc.HasValue && item.SentAt <= member.LastReadAtUtc.Value;
        }

        return page;
    }

    /// <summary>A page of <paramref name="q"/> (newest first in the query, returned oldest first), without hidden or deleted messages.</summary>
    private async Task<PagedMessagesResponse> LoadPageAsync(IQueryable<Message> q, Guid me, Guid? beforeId, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        DateTime? beforeSentAt = null;
        if (beforeId.HasValue)
        {
            var anchor = await _context.Messages.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == beforeId.Value);
            if (anchor != null) beforeSentAt = anchor.SentAt;
        }

        q = q.Where(m => !m.IsDeleted);
        var hiddenIds = _context.MessageHides
            .Where(h => h.UserId == me)
            .Select(h => h.MessageId);
        q = q.Where(m => !hiddenIds.Contains(m.Id));

        if (beforeSentAt.HasValue)
            q = q.Where(m => m.SentAt < beforeSentAt.Value);

        var rows = await q
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.Id)
            .Take(pageSize + 1)
            .ToListAsync();

        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        rows.Reverse();

        var byMsg = await LoadReactionsAsync(rows.Select(m => m.Id).ToList(), me);
        var items = rows.Select(m => ToResponse(m, byMsg)).ToList();

        return new PagedMessagesResponse
        {
            Items = items,
            HasMore = hasMore,
            OldestId = items.FirstOrDefault()?.MessageId.ToString()
        };
    }

    public async Task<MessageBriefDto> GetBriefAsync(Guid userId, Guid messageId)
    {
        var m = await VisibleTo(userId)
            .AsNoTracking()
            .Where(x => x.Id == messageId)
            .Select(x => new MessageBriefDto
            {
                MessageId = x.Id,
                SenderId = x.SenderId,
                ReceiverId = x.ReceiverId,
                GroupId = x.GroupId,
                EncryptedContent = x.EncryptedContent,
                FileUrl = x.FileUrl,
                SentAt = x.SentAt,
                ReplyToMessageId = x.ReplyToMessageId
            })
            .FirstOrDefaultAsync();

        return m ?? throw MessageNotFound();
    }

    public async Task<ReceivedMessageResponse> EditMessageAsync(Guid userId, Guid messageId, string encryptedText)
    {
        var m = await ParticipantOf(userId).FirstOrDefaultAsync(x => x.Id == messageId) ?? throw MessageNotFound();
        if (m.SenderId != userId || m.SystemEvent != null) throw new ForbiddenException("not_message_owner", "Only the sender can edit this message.");
        if (m.IsDeleted) throw new BadRequestException("message_deleted", "A deleted message cannot be edited.");

        var maxLength = m.GroupId.HasValue ? ValidationRules.GroupEncryptedTextMaxLength : ValidationRules.EncryptedTextMaxLength;
        if (string.IsNullOrEmpty(encryptedText) || encryptedText.Length > maxLength)
            throw new BadRequestException("invalid_message", "Message is empty or too large.");

        if (m.GroupId is { } groupId)
        {
            await _identityKeys.EnsureGroupMessageKeysAsync(userId, await MemberIdsAsync(groupId), encryptedText);
        }
        else
        {
            await _blocks.EnsureCanMessageAsync(userId, m.ReceiverId!.Value);
            await _identityKeys.EnsureMessageKeysAsync(userId, m.ReceiverId.Value, encryptedText);
        }

        m.EncryptedContent = encryptedText;
        m.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var readShared = m.GroupId == null && await _privacy.ReadReceiptsSharedAsync(m.SenderId, m.ReceiverId!.Value);
        return new ReceivedMessageResponse
        {
            MessageId = m.Id,
            SenderId = m.SenderId,
            ReceiverId = m.ReceiverId,
            GroupId = m.GroupId,
            EncryptedContent = m.EncryptedContent,
            FileUrl = m.FileUrl,
            SentAt = m.SentAt,
            DeliveredAtUtc = m.DeliveredAtUtc,
            ReadAtUtc = readShared ? m.ReadAtUtc : null,
            IsRead = readShared && m.IsRead,
            IsDeleted = m.IsDeleted,
            UpdatedAtUtc = m.UpdatedAtUtc
        };
    }

    public async Task DeleteMessageAsync(Guid userId, Guid messageId, string scope)
    {
        if (scope != "me" && scope != "all")
            throw new BadRequestException("invalid_scope", "Scope must be 'me' or 'all'.");

        var m = await ParticipantOf(userId).FirstOrDefaultAsync(x => x.Id == messageId) ?? throw MessageNotFound();

        if (scope == "all")
        {
            // Group admins may remove anyone's message; otherwise only the sender.
            var allowed = m.SenderId == userId && m.SystemEvent == null;
            if (!allowed && m.GroupId is { } groupId)
                allowed = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId && gm.Role != GroupRole.Member);
            if (!allowed) throw new ForbiddenException("not_message_owner", "Only the sender can delete a message for everyone.");

            m.IsDeleted = true;
            m.EncryptedContent = "";
            m.FileUrl = null;
            m.IsRead = true; // Mark as read when deleted
            m.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        else // "me"
        {
            var hide = await _context.MessageHides.FindAsync(userId, messageId);
            if (hide == null)
            {
                _context.MessageHides.Add(new MessageHide
                {
                    UserId = userId,
                    MessageId = messageId,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
        }
    }

    public async Task<MessageAudience?> GetAudienceAsync(Guid messageId)
    {
        var m = await _context.Messages
            .AsNoTracking()
            .Where(x => x.Id == messageId)
            .Select(x => new { x.SenderId, x.ReceiverId, x.GroupId })
            .FirstOrDefaultAsync();

        if (m == null) return null;
        return await AudienceOfAsync(m.SenderId, m.ReceiverId, m.GroupId);
    }

    private async Task<MessageAudience> AudienceOfAsync(Guid senderId, Guid? receiverId, Guid? groupId)
    {
        var users = groupId.HasValue
            ? await MemberIdsAsync(groupId.Value)
            : new List<Guid> { senderId, receiverId!.Value }.Distinct().ToList();
        return new MessageAudience(senderId, receiverId, groupId, users);
    }

    public async Task<int> AddReactionAsync(Guid userId, Guid messageId, string emoji)
    {
        emoji = emoji?.Trim() ?? "";
        if (string.IsNullOrEmpty(emoji))
            throw new BadRequestException("emoji_required", "Emoji is required.");

        await EnsureCanReactAsync(userId, messageId);

        var exists = await _context.MessageReactions.FindAsync(messageId, userId, emoji);
        if (exists == null)
        {
            _context.MessageReactions.Add(new MessageReaction
            {
                MessageId = messageId,
                UserId = userId,
                Emoji = emoji,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        return await CountReactionsAsync(messageId, emoji);
    }

    public async Task<int> RemoveReactionAsync(Guid userId, Guid messageId, string emoji)
    {
        emoji = emoji?.Trim() ?? "";
        await EnsureCanReactAsync(userId, messageId);

        var r = await _context.MessageReactions.FindAsync(messageId, userId, emoji);
        if (r != null)
        {
            _context.MessageReactions.Remove(r);
            await _context.SaveChangesAsync();
        }

        return await CountReactionsAsync(messageId, emoji);
    }

    // ---- pins ----

    public const int MaxPinsPerConversation = 50;

    public async Task<MessageAudience> PinAsync(Guid userId, Guid messageId)
    {
        var m = await VisibleMessageAsync(userId, messageId);
        if (m.SystemEvent != null) throw new BadRequestException("system_message", "Service messages cannot be pinned.");
        if (m.GroupId == null)
            await _blocks.EnsureCanMessageAsync(userId, m.SenderId == userId ? m.ReceiverId!.Value : m.SenderId);

        var audience = await AudienceOfAsync(m.SenderId, m.ReceiverId, m.GroupId);
        if (await _context.PinnedMessages.AnyAsync(p => p.MessageId == messageId))
            return audience;

        var pinsInConversation = m.GroupId is { } groupId
            ? await _context.PinnedMessages.CountAsync(p => p.Message.GroupId == groupId)
            : await _context.PinnedMessages.CountAsync(p => p.Message.GroupId == null &&
                ((p.Message.SenderId == m.SenderId && p.Message.ReceiverId == m.ReceiverId) ||
                 (p.Message.SenderId == m.ReceiverId && p.Message.ReceiverId == m.SenderId)));
        if (pinsInConversation >= MaxPinsPerConversation)
            throw new BadRequestException("too_many_pins", $"A conversation can have at most {MaxPinsPerConversation} pinned messages.");

        _context.PinnedMessages.Add(new PinnedMessage { MessageId = messageId, PinnedById = userId, PinnedAtUtc = DateTime.UtcNow });
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Pinned concurrently by another participant: same result.
        }
        return audience;
    }

    public async Task<MessageAudience> UnpinAsync(Guid userId, Guid messageId)
    {
        var m = await ParticipantOf(userId).AsNoTracking()
            .Where(x => x.Id == messageId)
            .Select(x => new { x.SenderId, x.ReceiverId, x.GroupId })
            .FirstOrDefaultAsync() ?? throw MessageNotFound();

        await _context.PinnedMessages.Where(p => p.MessageId == messageId).ExecuteDeleteAsync();
        return await AudienceOfAsync(m.SenderId, m.ReceiverId, m.GroupId);
    }

    public Task<List<PinnedMessageDto>> GetPinnedAsync(Guid userId, Guid peerId) =>
        PinnedDtos(userId, _context.PinnedMessages.AsNoTracking()
            .Where(p => (p.Message.SenderId == userId && p.Message.ReceiverId == peerId) ||
                        (p.Message.SenderId == peerId && p.Message.ReceiverId == userId)));

    public async Task<List<PinnedMessageDto>> GetGroupPinnedAsync(Guid userId, Guid groupId)
    {
        var member = await RequireMembershipAsync(groupId, userId);
        return await PinnedDtos(userId, _context.PinnedMessages.AsNoTracking()
            .Where(p => p.Message.GroupId == groupId && p.Message.SentAt >= member.JoinedAtUtc));
    }

    private Task<List<PinnedMessageDto>> PinnedDtos(Guid userId, IQueryable<PinnedMessage> pins) =>
        pins
            .Where(p => !p.Message.IsDeleted)
            .Where(p => !_context.MessageHides.Any(h => h.UserId == userId && h.MessageId == p.MessageId))
            .OrderByDescending(p => p.PinnedAtUtc)
            .Select(p => new PinnedMessageDto
            {
                MessageId = p.MessageId,
                SenderId = p.Message.SenderId,
                EncryptedContent = p.Message.EncryptedContent,
                FileUrl = p.Message.FileUrl,
                SentAt = p.Message.SentAt,
                PinnedById = p.PinnedById,
                PinnedAtUtc = p.PinnedAtUtc
            })
            .ToListAsync();

    /// <summary>A message of the user's conversations that is not deleted or hidden for them.</summary>
    private async Task<Message> VisibleMessageAsync(Guid userId, Guid messageId)
    {
        var m = await VisibleTo(userId).AsNoTracking()
            .Where(x => x.Id == messageId && !x.IsDeleted)
            .FirstOrDefaultAsync();
        return m ?? throw MessageNotFound();
    }

    // ---- access ----

    /// <summary>
    /// Messages of the user's chats: their private chats, and the groups they belong to (from when they joined).
    /// </summary>
    private IQueryable<Message> ParticipantOf(Guid userId) =>
        _context.Messages.Where(m =>
            (m.GroupId == null && (m.SenderId == userId || m.ReceiverId == userId)) ||
            (m.GroupId != null && _context.GroupMembers.Any(gm =>
                gm.GroupId == m.GroupId && gm.UserId == userId && m.SentAt >= gm.JoinedAtUtc)));

    /// <summary><see cref="ParticipantOf"/> without the messages the user deleted for themselves.</summary>
    private IQueryable<Message> VisibleTo(Guid userId) =>
        ParticipantOf(userId).Where(m => !_context.MessageHides.Any(h => h.UserId == userId && h.MessageId == m.Id));

    private async Task<GroupMember> RequireMembershipAsync(Guid groupId, Guid userId) =>
        await _context.GroupMembers.AsNoTracking().FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId)
        ?? throw GroupNotFound();

    private Task<List<Guid>> MemberIdsAsync(Guid groupId) =>
        _context.GroupMembers.Where(gm => gm.GroupId == groupId).Select(gm => gm.UserId).ToListAsync();

    // ---- helpers ----

    private sealed record ForwardSource(Guid MessageId, Guid OriginalSenderId, string? FileUrl);

    /// <summary>
    /// The sender may forward a message they can see: one of their own conversation's messages,
    /// or the original of a forward they received (forwarding a forward keeps pointing at the original).
    /// </summary>
    private async Task<ForwardSource> ResolveForwardSourceAsync(Guid userId, Guid sourceId)
    {
        var visible = await VisibleTo(userId)
            .AsNoTracking()
            .Where(m => (m.Id == sourceId || m.ForwardedFromMessageId == sourceId) && !m.IsDeleted && m.SystemEvent == null)
            .OrderByDescending(m => m.Id == sourceId)
            .Select(m => new { m.Id, m.SenderId, m.ForwardedFromSenderId, m.FileUrl })
            .FirstOrDefaultAsync();

        if (visible == null)
            throw new NotFoundException("forward_source_not_found", "The message to forward was not found.");

        var originalSender = visible.Id == sourceId
            ? visible.SenderId
            : visible.ForwardedFromSenderId ?? visible.SenderId;

        return new ForwardSource(sourceId, originalSender, visible.FileUrl);
    }

    private async Task EnsureReceiverExistsAsync(Guid receiverId)
    {
        if (!await _context.Users.AnyAsync(x => x.Id == receiverId))
            throw new NotFoundException("receiver_not_found", "Receiver not found.");
    }

    /// <summary>A reply must point at a message of the same conversation.</summary>
    private async Task EnsureReplyTargetAsync(Guid senderId, ChatTarget target, Guid? replyToMessageId)
    {
        if (!replyToMessageId.HasValue) return;

        var ok = target.GroupId is { } groupId
            ? await VisibleTo(senderId).AnyAsync(m => m.Id == replyToMessageId.Value && m.GroupId == groupId && m.SystemEvent == null)
            : await _context.Messages.AnyAsync(m =>
                m.Id == replyToMessageId.Value &&
                ((m.SenderId == senderId && m.ReceiverId == target.ReceiverId) ||
                 (m.SenderId == target.ReceiverId && m.ReceiverId == senderId)));

        if (!ok)
            throw new BadRequestException("invalid_reply", "The replied message does not belong to this conversation.");
    }

    private async Task EnsureCanReactAsync(Guid userId, Guid messageId)
    {
        var m = await ParticipantOf(userId)
            .AsNoTracking()
            .Where(x => x.Id == messageId)
            .Select(x => new { x.SenderId, x.ReceiverId, x.GroupId, x.IsDeleted, x.SystemEvent })
            .FirstOrDefaultAsync();

        if (m == null || m.IsDeleted || m.SystemEvent != null)
            throw MessageNotFound();

        if (m.GroupId == null)
            await _blocks.EnsureCanMessageAsync(userId, m.SenderId == userId ? m.ReceiverId!.Value : m.SenderId);
    }

    private Task<int> CountReactionsAsync(Guid messageId, string emoji) =>
        _context.MessageReactions.CountAsync(r => r.MessageId == messageId && r.Emoji == emoji);

    private async Task<Dictionary<Guid, List<ReactionSummaryDto>>> LoadReactionsAsync(List<Guid> msgIds, Guid currentUserId)
    {
        if (msgIds.Count == 0)
            return new Dictionary<Guid, List<ReactionSummaryDto>>();

        // Reactions: total count + the user's own reactions
        var grouped = await _context.MessageReactions
            .Where(r => msgIds.Contains(r.MessageId))
            .GroupBy(r => new { r.MessageId, r.Emoji })
            .Select(g => new { g.Key.MessageId, g.Key.Emoji, Count = g.Count() })
            .ToListAsync();

        var myReacts = await _context.MessageReactions
            .Where(r => msgIds.Contains(r.MessageId) && r.UserId == currentUserId)
            .ToListAsync();

        return grouped
            .GroupBy(x => x.MessageId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new ReactionSummaryDto
                {
                    Emoji = x.Emoji,
                    Count = x.Count,
                    Mine = myReacts.Any(mr => mr.MessageId == x.MessageId && mr.Emoji == x.Emoji)
                }).ToList()
            );
    }

    private static ReceivedMessageResponse ToResponse(Message m, Dictionary<Guid, List<ReactionSummaryDto>> reactions) => new()
    {
        MessageId = m.Id,
        SenderId = m.SenderId,
        ReceiverId = m.ReceiverId,
        GroupId = m.GroupId,
        SystemEvent = m.SystemEvent,
        EncryptedContent = m.EncryptedContent,
        SentAt = m.SentAt,
        FileUrl = m.FileUrl,
        IsRead = m.IsRead,
        DeliveredAtUtc = m.DeliveredAtUtc,
        ReadAtUtc = m.ReadAtUtc,
        ReplyToMessageId = m.ReplyToMessageId,
        IsDeleted = m.IsDeleted,
        UpdatedAtUtc = m.UpdatedAtUtc,
        Reactions = reactions.TryGetValue(m.Id, out var list) ? list : new List<ReactionSummaryDto>(),
        ForwardedFromMessageId = m.ForwardedFromMessageId,
        ForwardedFromSenderId = m.ForwardedFromSenderId
    };

    private static NotFoundException MessageNotFound() =>
        new("message_not_found", "Message not found.");

    private static NotFoundException GroupNotFound() =>
        new("group_not_found", "Group not found.");
}
