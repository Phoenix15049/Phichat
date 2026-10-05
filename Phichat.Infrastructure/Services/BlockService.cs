using Microsoft.EntityFrameworkCore;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.Interfaces;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;

namespace Phichat.Infrastructure.Services;

public class BlockService : IBlockService
{
    private readonly AppDbContext _db;

    public BlockService(AppDbContext db)
    {
        _db = db;
    }

    public async Task BlockAsync(Guid blockerId, Guid blockedId)
    {
        if (blockerId == blockedId)
            throw new BadRequestException("cannot_block_self", "You cannot block yourself.");

        if (!await _db.Users.AnyAsync(u => u.Id == blockedId))
            throw new NotFoundException("user_not_found", "User not found.");

        if (await _db.UserBlocks.AnyAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId))
            return;

        _db.UserBlocks.Add(new UserBlock { BlockerId = blockerId, BlockedId = blockedId, CreatedAtUtc = DateTime.UtcNow });
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent request blocked first: the result is the same.
        }
    }

    public async Task UnblockAsync(Guid blockerId, Guid blockedId)
    {
        await _db.UserBlocks
            .Where(b => b.BlockerId == blockerId && b.BlockedId == blockedId)
            .ExecuteDeleteAsync();
    }

    public Task<List<BlockedUserDto>> GetBlockedAsync(Guid blockerId) =>
        _db.UserBlocks.AsNoTracking()
            .Where(b => b.BlockerId == blockerId)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new BlockedUserDto
            {
                UserId = b.BlockedId,
                Username = b.Blocked.Username,
                DisplayName = b.Blocked.DisplayName,
                AvatarUrl = b.Blocked.AvatarUrl,
                BlockedAtUtc = b.CreatedAtUtc
            })
            .ToListAsync();

    public Task<bool> IsBlockedEitherWayAsync(Guid a, Guid b) =>
        _db.UserBlocks.AnyAsync(x => (x.BlockerId == a && x.BlockedId == b) || (x.BlockerId == b && x.BlockedId == a));

    public async Task EnsureCanMessageAsync(Guid senderId, Guid receiverId)
    {
        if (senderId == receiverId) return;

        var blockers = await _db.UserBlocks.AsNoTracking()
            .Where(x => (x.BlockerId == senderId && x.BlockedId == receiverId) || (x.BlockerId == receiverId && x.BlockedId == senderId))
            .Select(x => x.BlockerId)
            .ToListAsync();

        if (blockers.Contains(senderId))
            throw new ForbiddenException("user_blocked", "You blocked this user. Unblock them to send messages.");

        if (blockers.Count > 0)
            throw new ForbiddenException("cannot_message_user", "You cannot send messages to this user.");
    }
}
