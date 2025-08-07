using Microsoft.EntityFrameworkCore;
using Phichat.Application.Interfaces;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;

namespace Phichat.Infrastructure.Services;

public class ChatKeyService : IChatKeyService
{
    private readonly AppDbContext _db;

    public ChatKeyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task StoreChatKeyAsync(Guid senderId, Guid receiverId, byte[] key)
    {
        var existsForward = await _db.ChatKeys
            .AnyAsync(x => x.SenderId == senderId && x.ReceiverId == receiverId);

        var existsBackward = await _db.ChatKeys
            .AnyAsync(x => x.SenderId == receiverId && x.ReceiverId == senderId);

        if (!existsForward)
        {
            var chatKey = new ChatKey
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                SymmetricKey = key,
                CreatedAt = DateTime.UtcNow
            };
            _db.ChatKeys.Add(chatKey);
        }

        if (!existsBackward)
        {
            var reverseChatKey = new ChatKey
            {
                SenderId = receiverId,
                ReceiverId = senderId,
                SymmetricKey = key,
                CreatedAt = DateTime.UtcNow
            };
            _db.ChatKeys.Add(reverseChatKey);
        }

        await _db.SaveChangesAsync();
    }

    public async Task<byte[]?> GetEncryptedChatKeyAsync(Guid requestorId, Guid otherUserId)
    {
        return await _db.ChatKeys
            .Where(x => x.SenderId == otherUserId && x.ReceiverId == requestorId)
            .Select(x => x.SymmetricKey)
            .FirstOrDefaultAsync();
    }
}
