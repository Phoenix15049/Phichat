using Microsoft.EntityFrameworkCore;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.Interfaces;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;

namespace Phichat.Infrastructure.Services;

public sealed class MuteService : IMuteService
{
    private readonly AppDbContext _db;

    public MuteService(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<Guid>> GetMutedAsync(Guid userId) =>
        _db.ChatMutes.AsNoTracking().Where(m => m.UserId == userId).Select(m => m.ChatId).ToListAsync();

    public async Task MuteAsync(Guid userId, Guid chatId)
    {
        // A private chat (the other user) or a group the user belongs to.
        var exists = await _db.Users.AnyAsync(u => u.Id == chatId)
            || await _db.GroupMembers.AnyAsync(gm => gm.GroupId == chatId && gm.UserId == userId);
        if (!exists)
            throw new NotFoundException("chat_not_found", "Chat not found.");

        if (await IsMutedAsync(userId, chatId)) return;

        _db.ChatMutes.Add(new ChatMute { UserId = userId, ChatId = chatId, CreatedAtUtc = DateTime.UtcNow });
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Muted concurrently from another device: same result.
        }
    }

    public async Task UnmuteAsync(Guid userId, Guid chatId) =>
        await _db.ChatMutes.Where(m => m.UserId == userId && m.ChatId == chatId).ExecuteDeleteAsync();

    public Task<bool> IsMutedAsync(Guid userId, Guid chatId) =>
        _db.ChatMutes.AnyAsync(m => m.UserId == userId && m.ChatId == chatId);
}
