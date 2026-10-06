namespace Phichat.Application.Interfaces;

/// <summary>Muted chats (private chats by the other user's id, groups by the group id). Shared by all of the user's devices.</summary>
public interface IMuteService
{
    Task<List<Guid>> GetMutedAsync(Guid userId);
    Task MuteAsync(Guid userId, Guid chatId);
    Task UnmuteAsync(Guid userId, Guid chatId);
    Task<bool> IsMutedAsync(Guid userId, Guid chatId);
}
