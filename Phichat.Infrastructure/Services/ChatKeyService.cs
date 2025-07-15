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

    public async Task StoreChatKeyAsync(Guid senderId, Guid receiverId, byte[] encryptedKey)
    {
        var exists = await _db.ChatKeys
            .AnyAsync(x => x.SenderId == senderId && x.ReceiverId == receiverId);

        if (!exists)
        {
            var chatKey = new ChatKey
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                EncryptedSymmetricKey = encryptedKey
            };
            _db.ChatKeys.Add(chatKey);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<byte[]?> GetEncryptedChatKeyAsync(Guid requestorId, Guid otherUserId)
    {
        return await _db.ChatKeys
            .Where(x => x.SenderId == otherUserId && x.ReceiverId == requestorId)
            .Select(x => x.EncryptedSymmetricKey)
            .FirstOrDefaultAsync();
    }
}
