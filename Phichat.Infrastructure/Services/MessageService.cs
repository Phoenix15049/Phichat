using Microsoft.EntityFrameworkCore;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Interfaces;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;

public class MessageService : IMessageService
{
    private readonly AppDbContext _context;
    private readonly IEncryptionService _encryption;

    public MessageService(AppDbContext context, IEncryptionService encryption)
    {
        _context = context;
        _encryption = encryption;
    }

    public async Task SendMessageAsync(Guid senderId, SendMessageRequest request)
    {
        var receiver = await _context.Users.FirstOrDefaultAsync(x => x.Id == request.ReceiverId);
        if (receiver == null)
            throw new Exception("Receiver not found.");

        var encrypted = _encryption.EncryptWithPublicKey(receiver.PublicKey, request.PlainText);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            ReceiverId = request.ReceiverId,
            EncryptedContent = encrypted,
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
}
