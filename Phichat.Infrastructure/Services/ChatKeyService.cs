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
        await using var tx = await _db.Database.BeginTransactionAsync();

        var forward = await _db.ChatKeys
            .FirstOrDefaultAsync(x => x.SenderId == senderId && x.ReceiverId == receiverId);

        var backward = await _db.ChatKeys
            .FirstOrDefaultAsync(x => x.SenderId == receiverId && x.ReceiverId == senderId);

        if (forward == null && backward == null)
        {
            _db.ChatKeys.Add(new ChatKey
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                SymmetricKey = key,
                CreatedAt = DateTime.UtcNow
            });
            _db.ChatKeys.Add(new ChatKey
            {
                SenderId = receiverId,
                ReceiverId = senderId,
                SymmetricKey = key,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            if (forward != null) forward.SymmetricKey = key;
            else _db.ChatKeys.Add(new ChatKey
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                SymmetricKey = key,
                CreatedAt = DateTime.UtcNow
            });

            if (backward != null) backward.SymmetricKey = key;
            else _db.ChatKeys.Add(new ChatKey
            {
                SenderId = receiverId,
                ReceiverId = senderId,
                SymmetricKey = key,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
    }


    public async Task<byte[]?> GetEncryptedChatKeyAsync(Guid requestorId, Guid otherUserId)
    {
        return await _db.ChatKeys
            .Where(x => x.SenderId == otherUserId && x.ReceiverId == requestorId)
            .Select(x => x.SymmetricKey)
            .FirstOrDefaultAsync();
    }
}
