namespace Phichat.Application.Interfaces;

public class BlockedUserDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = default!;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime BlockedAtUtc { get; set; }
}

/// <summary>Blocking users: blocked pairs exchange no messages, typing or presence.</summary>
public interface IBlockService
{
    /// <summary>Blocking someone already blocked is a no-op.</summary>
    Task BlockAsync(Guid blockerId, Guid blockedId);

    Task UnblockAsync(Guid blockerId, Guid blockedId);

    Task<List<BlockedUserDto>> GetBlockedAsync(Guid blockerId);

    /// <summary>True when either user blocked the other.</summary>
    Task<bool> IsBlockedEitherWayAsync(Guid a, Guid b);

    /// <summary>
    /// Throws when a message from <paramref name="senderId"/> to <paramref name="receiverId"/> is not allowed:
    /// <c>user_blocked</c> if the sender blocked the receiver (they can unblock), otherwise
    /// <c>cannot_message_user</c> (which does not reveal that the receiver blocked them).
    /// </summary>
    Task EnsureCanMessageAsync(Guid senderId, Guid receiverId);
}
