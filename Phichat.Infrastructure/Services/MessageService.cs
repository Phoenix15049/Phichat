using Microsoft.EntityFrameworkCore;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Interfaces;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;

public class MessageService : IMessageService
{
    private readonly AppDbContext _context;

    public MessageService(AppDbContext context)
    {
        _context = context;

    }

    public async Task SendMessageAsync(Guid senderId, SendMessageRequest request)
    {
        var receiver = await _context.Users.FirstOrDefaultAsync(x => x.Id == request.ReceiverId);
        if (receiver == null)
            throw new Exception("Receiver not found.");


        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            ReceiverId = request.ReceiverId,
            EncryptedContent = request.EncryptedText,
            FileUrl = request.FileUrl,
            SentAt = DateTime.UtcNow,
            ReplyToMessageId = request.ReplyToMessageId


        };

        message.DeliveredAtUtc = DateTime.UtcNow;

        if (request.ForwardedFromMessageId.HasValue)
        {
            var src = await _context.Messages
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.ForwardedFromMessageId.Value);

            if (src != null)
            {
                message.ForwardedFromMessageId = src.Id;
                message.ForwardedFromSenderId = src.SenderId;
                // توجه: متن و فایل را کلاینت برای مقصد "رمزنگاری مجدد" می‌فرستد
            }
        }



        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
    }

    public async Task<List<ReceivedMessageResponse>> GetConversationAsync(Guid currentUserId, Guid otherUserId)
    {
        // پیام‌های «حذف برای من» را حذف کن
        var hiddenIds = _context.MessageHides
            .Where(h => h.UserId == currentUserId)
            .Select(h => h.MessageId);

        // بدنه‌ی گفتگو (بدون IsDeleted و بدون مخفی‌ها)
        var rows = await _context.Messages.AsNoTracking()
            .Where(m =>
                ((m.SenderId == currentUserId && m.ReceiverId == otherUserId) ||
                 (m.SenderId == otherUserId && m.ReceiverId == currentUserId)) &&
                !m.IsDeleted &&
                !hiddenIds.Contains(m.Id))
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        if (rows.Count == 0)
            return new List<ReceivedMessageResponse>();

        // واکنش‌ها: شمارش کلی + واکنش‌های خود کاربر
        var msgIds = rows.Select(m => m.Id).ToList();

        var grouped = await _context.MessageReactions
            .Where(r => msgIds.Contains(r.MessageId))
            .GroupBy(r => new { r.MessageId, r.Emoji })
            .Select(g => new { g.Key.MessageId, g.Key.Emoji, Count = g.Count() })
            .ToListAsync();

        var myReacts = await _context.MessageReactions
            .Where(r => msgIds.Contains(r.MessageId) && r.UserId == currentUserId)
            .ToListAsync();

        var byMsg = grouped
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

        // مپ به DTO نهایی
        var items = rows.Select(m => new ReceivedMessageResponse
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
            Reactions = byMsg.ContainsKey(m.Id) ? byMsg[m.Id] : new List<ReactionSummaryDto>(),
            ForwardedFromMessageId = m.ForwardedFromMessageId,
            ForwardedFromSenderId = m.ForwardedFromSenderId
        }).ToList();

        return items;
    }




    public async Task SendMessageWithFileAsync(Guid senderId, SendMessageWithFileRequest request, string uploadRootPath)
    {
        var receiver = await _context.Users.FirstOrDefaultAsync(x => x.Id == request.ReceiverId);
        if (receiver == null)
            throw new Exception("Receiver not found.");

        string? encryptedText = null;
        encryptedText = request.EncryptedText;
        string savedPath = string.Empty;
        if (request.File != null && request.File.Length > 0)
        {
            var fileName = $"{Guid.NewGuid()}_{request.File.FileName}";
            var fullPath = Path.Combine(uploadRootPath, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

            savedPath = $"/uploads/{fileName}";
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            ReceiverId = request.ReceiverId,
            EncryptedContent = encryptedText ?? "",
            FileUrl = savedPath,
            SentAt = DateTime.UtcNow,
            ReplyToMessageId = request.ReplyToMessageId

        };

        message.DeliveredAtUtc = DateTime.UtcNow;

        if (request.ForwardedFromMessageId.HasValue)
        {
            var src = await _context.Messages
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.ForwardedFromMessageId.Value);

            if (src != null)
            {
                message.ForwardedFromMessageId = src.Id;
                message.ForwardedFromSenderId = src.SenderId;
                // توجه: متن و فایل را کلاینت برای مقصد "رمزنگاری مجدد" می‌فرستد
            }
        }


        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
    }

    public async Task SendMessageFromHubAsync(Guid senderId, SendMessageViaHubRequest request, string uploadRootPath)
    {
        var receiver = await _context.Users.FirstOrDefaultAsync(x => x.Id == request.ReceiverId);
        if (receiver == null)
            throw new Exception("Receiver not found");

        string encryptedContent = request.EncryptedText;
        string? fileUrl = null;

        if (!string.IsNullOrEmpty(request.FileBase64) && !string.IsNullOrEmpty(request.FileName))
        {
            var bytes = Convert.FromBase64String(request.FileBase64);
            var uniqueFileName = $"{Guid.NewGuid()}_{request.FileName}";
            var fullPath = Path.Combine(uploadRootPath, uniqueFileName);
            await File.WriteAllBytesAsync(fullPath, bytes);
            fileUrl = $"/uploads/{uniqueFileName}";
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            ReceiverId = request.ReceiverId,
            EncryptedContent = encryptedContent,
            FileUrl = fileUrl,
            SentAt = DateTime.UtcNow,
            ReplyToMessageId = request.ReplyToMessageId
        };

        message.DeliveredAtUtc = DateTime.UtcNow;

        if (request.ForwardedFromMessageId.HasValue)
        {
            var src = await _context.Messages
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.ForwardedFromMessageId.Value);

            if (src != null)
            {
                message.ForwardedFromMessageId = src.Id;
                message.ForwardedFromSenderId = src.SenderId;
                // توجه: متن و فایل را کلاینت برای مقصد "رمزنگاری مجدد" می‌فرستد
            }
        }



        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
    }


    public async Task<MessageReadResult> MarkAsReadAsync(Guid messageId, Guid readerId)
    {
        var message = await _context.Messages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ReceiverId == readerId);

        if (message == null || message.IsRead)
            return new MessageReadResult { Success = false };

        message.IsRead = true;
        await _context.SaveChangesAsync();

        return new MessageReadResult
        {
            Success = true,
            SenderId = message.SenderId,
            MessageId = message.Id,            
            ReadAtUtc = message.ReadAtUtc

        };
    }


    public async Task<Message?> GetLastMessageBetweenAsync(Guid senderId, Guid receiverId)
    {
        return await _context.Messages
            .Where(m => m.SenderId == senderId && m.ReceiverId == receiverId)
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync();
    }


    public async Task<List<ReceivedMessageResponse>> GetReceivedMessagesAsync(Guid receiverId)
    {
        return await _context.Messages
            .Where(m => m.ReceiverId == receiverId)
            .OrderByDescending(m => m.SentAt)
            .Select(m => new ReceivedMessageResponse
            {
                MessageId = m.Id,
                SenderId = m.SenderId,
                EncryptedContent = m.EncryptedContent,
                SentAt = m.SentAt,
                FileUrl = m.FileUrl,
                ReplyToMessageId = m.ReplyToMessageId,
                IsDeleted = m.IsDeleted,
                UpdatedAtUtc = m.UpdatedAtUtc,
            }).ToListAsync();
    }


    public async Task<List<ConversationDto>> GetConversationsAsync(Guid currentUserId)
    {
        var baseQuery = _context.Messages
            .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
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

        var msgIds = rows.Select(m => m.Id).ToList();

        var grouped = await _context.MessageReactions
            .Where(r => msgIds.Contains(r.MessageId))
            .GroupBy(r => new { r.MessageId, r.Emoji })
            .Select(g => new { g.Key.MessageId, g.Key.Emoji, Count = g.Count() })
            .ToListAsync();

        var myReacts = await _context.MessageReactions
            .Where(r => msgIds.Contains(r.MessageId) && r.UserId == me /* یا currentUserId */)
            .ToListAsync();

        var byMsg = grouped.GroupBy(x => x.MessageId).ToDictionary(
            g => g.Key,
            g => g.Select(x => new ReactionSummaryDto
            {
                Emoji = x.Emoji,
                Count = x.Count,
                Mine = myReacts.Any(mr => mr.MessageId == x.MessageId && mr.Emoji == x.Emoji)
            }).ToList()
        );

        var items = rows.Select(m => new ReceivedMessageResponse
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
            Reactions = byMsg.ContainsKey(m.Id) ? byMsg[m.Id] : new List<ReactionSummaryDto>(),
            ForwardedFromMessageId = m.ForwardedFromMessageId,
            ForwardedFromSenderId = m.ForwardedFromSenderId
        }).ToList();

        return new PagedMessagesResponse
        {
            Items = items,
            HasMore = hasMore,
            OldestId = items.FirstOrDefault()?.MessageId.ToString()
        };
    }





    public async Task<ReceivedMessageResponse> EditMessageAsync(Guid userId, Guid messageId, string encryptedText)
    {
        var m = await _context.Messages.FirstOrDefaultAsync(x => x.Id == messageId);
        if (m == null) throw new Exception("Message not found.");
        if (m.SenderId != userId) throw new Exception("Not allowed.");

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
        var m = await _context.Messages.FirstOrDefaultAsync(x => x.Id == messageId);
        if (m == null) return;

        if (scope == "all")
        {
            if (m.SenderId != userId) throw new Exception("Not allowed.");
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


    public async Task AddReactionAsync(Guid userId, Guid messageId, string emoji)
    {
        emoji = emoji?.Trim() ?? "";
        if (string.IsNullOrEmpty(emoji)) return;

        var exists = await _context.MessageReactions.FindAsync(messageId, userId, emoji);
        if (exists != null) return;

        _context.MessageReactions.Add(new MessageReaction
        {
            MessageId = messageId,
            UserId = userId,
            Emoji = emoji,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    public async Task RemoveReactionAsync(Guid userId, Guid messageId, string emoji)
    {
        var r = await _context.MessageReactions.FindAsync(messageId, userId, emoji);
        if (r == null) return;
        _context.MessageReactions.Remove(r);
        await _context.SaveChangesAsync();
    }




}
