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


        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
    }

    public async Task<List<ReceivedMessageResponse>> GetConversationAsync(Guid currentUserId, Guid otherUserId)
    {
        return await _context.Messages
            .Where(m =>
                (m.SenderId == currentUserId && m.ReceiverId == otherUserId) ||
                (m.SenderId == otherUserId && m.ReceiverId == currentUserId)
            )
            .OrderBy(m => m.SentAt)
            .Select(m => new ReceivedMessageResponse
            {
                MessageId = m.Id,
                SenderId = m.SenderId,
                EncryptedContent = m.EncryptedContent,
                SentAt = m.SentAt,
                FileUrl = m.FileUrl,
                IsRead = m.IsRead,
                ReplyToMessageId = m.ReplyToMessageId
            })
            .ToListAsync();
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
                ReplyToMessageId = m.ReplyToMessageId
            }).ToListAsync();
    }


    public async Task<List<ConversationDto>> GetConversationsAsync(Guid currentUserId)
    {
        // Base query for user's messages
        var baseQuery = _context.Messages
            .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
            .Select(m => new
            {
                PeerId = m.SenderId == currentUserId ? m.ReceiverId : m.SenderId,
                Msg = m
            });

        // Group by peer and compute last message + unread count
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

        if (beforeSentAt.HasValue)
            q = q.Where(m => m.SentAt < beforeSentAt.Value);

        // pageSize+1 برای تشخیص HasMore
        var rows = await q
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.Id)
            .Take(pageSize + 1)
            .ToListAsync();

        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1); // oldest extra

        // به ترتیب صعودی برای UI
        rows.Reverse();

        var items = rows.Select(m => new ReceivedMessageResponse
        {
            MessageId = m.Id,
            SenderId = m.SenderId,
            EncryptedContent = m.EncryptedContent,
            SentAt = m.SentAt,
            FileUrl = m.FileUrl,
            IsRead = m.IsRead,
            DeliveredAtUtc = m.DeliveredAtUtc,
            ReadAtUtc = m.ReadAtUtc,
            ReplyToMessageId = m.ReplyToMessageId

        }).ToList();

        return new PagedMessagesResponse
        {
            Items = items,
            HasMore = hasMore,
            OldestId = items.FirstOrDefault()?.MessageId.ToString()
        };
    }


}
