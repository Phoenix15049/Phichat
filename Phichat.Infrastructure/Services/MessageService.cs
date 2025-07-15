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
            SentAt = DateTime.UtcNow
        };


        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
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
                SentAt = m.SentAt
            }).ToListAsync();
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
            SentAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
    }

    public async Task SendMessageFromHubAsync(Guid senderId, SendMessageViaHubRequest request, string uploadRootPath)
    {
        var receiver = await _context.Users.FirstOrDefaultAsync(x => x.Id == request.ReceiverId);
        if (receiver == null)
            throw new Exception("Receiver not found");

        
        string? fileUrl = null;

        string encryptedContent = request.EncryptedText;

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
            SentAt = DateTime.UtcNow
        };

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
            SenderId = message.SenderId
        };
    }



}
