using Microsoft.EntityFrameworkCore;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Validators;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;
using Phichat.Infrastructure.Files;

public class MessageService : IMessageService
{
    private readonly AppDbContext _context;

    public MessageService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Message> SendMessageAsync(Guid senderId, SendMessageRequest request)
    {
        // Hub calls bypass MVC model validation, so the body limits are enforced here too.
        if (string.IsNullOrEmpty(request.EncryptedText) || request.EncryptedText.Length > ValidationRules.EncryptedTextMaxLength)
            throw new BadRequestException("invalid_message", "Message is empty or too large.");

        await EnsureReceiverExistsAsync(request.ReceiverId);
        await EnsureReplyTargetAsync(senderId, request.ReceiverId, request.ReplyToMessageId);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            ReceiverId = request.ReceiverId,
            EncryptedContent = request.EncryptedText,
            SentAt = DateTime.UtcNow,
            ReplyToMessageId = request.ReplyToMessageId
        };

        message.DeliveredAtUtc = DateTime.UtcNow;

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
        await EnsureReceiverExistsAsync(request.ReceiverId);
        await EnsureReplyTargetAsync(senderId, request.ReceiverId, request.ReplyToMessageId);

        if (request.File == null || request.File.Length == 0)
            throw new BadRequestException("file_required", "File is required.");

        // Random prefix + sanitized name: the client name is kept for display but can never escape the folder.
        var storedName = $"{Guid.NewGuid():N}_{FileNameSanitizer.Sanitize(request.File.FileName)}";
        var fullPath = Path.Combine(uploadRootPath, storedName);

        await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
        {
            await request.File.CopyToAsync(stream);
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            ReceiverId = request.ReceiverId,
            EncryptedContent = request.EncryptedText ?? "",
            FileUrl = "/uploads/" + Uri.EscapeDataString(storedName),
            SentAt = DateTime.UtcNow,
            ReplyToMessageId = request.ReplyToMessageId
        };

        message.DeliveredAtUtc = DateTime.UtcNow;

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

    public async Task<List<ConversationDto>> GetConversationsAsync(Guid currentUserId)
    {
        // Only messages the user can still see: not deleted for everyone, not hidden ("delete for me").
        var hiddenIds = _context.MessageHides
            .Where(h => h.UserId == currentUserId)
            .Select(h => h.MessageId);

        var baseQuery = _context.Messages
            .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
            .Where(m => !m.IsDeleted && !hiddenIds.Contains(m.Id))
            .Select(m => new
            {
                PeerId = m.SenderId == currentUserId ? m.ReceiverId : m.SenderId,
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
                    LastEncryptedContent = x.Last.EncryptedContent,
                    LastFileUrl = x.Last.FileUrl,
                    LastSentAt = x.Last.SentAt,
                    UnreadCount = x.Unread
                };
            })
            .OrderByDescending(c => c.LastSentAt)
            .ToList();

        return result;
    }

    public async Task<PagedMessagesResponse> GetConversationPageAsync(Guid me, Guid other, Guid? beforeId, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        DateTime? beforeSentAt = null;
        if (beforeId.HasValue)
        {
            var anchor = await _context.Messages.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == beforeId.Value);
            if (anchor != null) beforeSentAt = anchor.SentAt;
        }

        var q = _context.Messages.AsNoTracking()
            .Where(m => (m.SenderId == me && m.ReceiverId == other) || (m.SenderId == other && m.ReceiverId == me));
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
        var m = await _context.Messages
            .AsNoTracking()
            .Where(x => x.Id == messageId && (x.SenderId == userId || x.ReceiverId == userId))
            .Where(x => !_context.MessageHides.Any(h => h.UserId == userId && h.MessageId == x.Id))
            .Select(x => new MessageBriefDto
            {
                MessageId = x.Id,
                SenderId = x.SenderId,
                ReceiverId = x.ReceiverId,
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
        var m = await _context.Messages.FirstOrDefaultAsync(x => x.Id == messageId);
        if (m == null || (m.SenderId != userId && m.ReceiverId != userId)) throw MessageNotFound();
        if (m.SenderId != userId) throw new ForbiddenException("not_message_owner", "Only the sender can edit this message.");
        if (m.IsDeleted) throw new BadRequestException("message_deleted", "A deleted message cannot be edited.");

        m.EncryptedContent = encryptedText ?? "";
        m.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new ReceivedMessageResponse
        {
            MessageId = m.Id,
            SenderId = m.SenderId,
            EncryptedContent = m.EncryptedContent,
            FileUrl = m.FileUrl,
            SentAt = m.SentAt,
            DeliveredAtUtc = m.DeliveredAtUtc,
            ReadAtUtc = m.ReadAtUtc,
            IsRead = m.IsRead,
            IsDeleted = m.IsDeleted,
            UpdatedAtUtc = m.UpdatedAtUtc
        };
    }

    public async Task DeleteMessageAsync(Guid userId, Guid messageId, string scope)
    {
        if (scope != "me" && scope != "all")
            throw new BadRequestException("invalid_scope", "Scope must be 'me' or 'all'.");

        var m = await _context.Messages.FirstOrDefaultAsync(x => x.Id == messageId);
        if (m == null || (m.SenderId != userId && m.ReceiverId != userId)) throw MessageNotFound();

        if (scope == "all")
        {
            if (m.SenderId != userId) throw new ForbiddenException("not_message_owner", "Only the sender can delete a message for everyone.");
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

    public async Task<(Guid SenderId, Guid ReceiverId)?> GetPeerIdsForMessageAsync(Guid messageId)
    {
        var m = await _context.Messages
            .AsNoTracking()
            .Where(x => x.Id == messageId)
            .Select(x => new { x.SenderId, x.ReceiverId })
            .FirstOrDefaultAsync();

        if (m == null) return null;
        return (m.SenderId, m.ReceiverId);
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

    // ---- helpers ----

    private sealed record ForwardSource(Guid MessageId, Guid OriginalSenderId, string? FileUrl);

    /// <summary>
    /// The sender may forward a message they can see: one of their own conversation's messages,
    /// or the original of a forward they received (forwarding a forward keeps pointing at the original).
    /// </summary>
    private async Task<ForwardSource> ResolveForwardSourceAsync(Guid userId, Guid sourceId)
    {
        var visible = await _context.Messages
            .AsNoTracking()
            .Where(m => (m.Id == sourceId || m.ForwardedFromMessageId == sourceId)
                        && (m.SenderId == userId || m.ReceiverId == userId)
                        && !m.IsDeleted)
            .Where(m => !_context.MessageHides.Any(h => h.UserId == userId && h.MessageId == m.Id))
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
    private async Task EnsureReplyTargetAsync(Guid senderId, Guid receiverId, Guid? replyToMessageId)
    {
        if (!replyToMessageId.HasValue) return;

        var ok = await _context.Messages.AnyAsync(m =>
            m.Id == replyToMessageId.Value &&
            ((m.SenderId == senderId && m.ReceiverId == receiverId) ||
             (m.SenderId == receiverId && m.ReceiverId == senderId)));

        if (!ok)
            throw new BadRequestException("invalid_reply", "The replied message does not belong to this conversation.");
    }

    private async Task EnsureCanReactAsync(Guid userId, Guid messageId)
    {
        var m = await _context.Messages
            .AsNoTracking()
            .Where(x => x.Id == messageId)
            .Select(x => new { x.SenderId, x.ReceiverId, x.IsDeleted })
            .FirstOrDefaultAsync();

        if (m == null || (m.SenderId != userId && m.ReceiverId != userId) || m.IsDeleted)
            throw MessageNotFound();
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
}
