namespace Phichat.Application.Interfaces
{
    public interface IChatKeyService
    {
        Task StoreChatKeyAsync(Guid senderId, Guid receiverId, byte[] key);
        Task<byte[]?> GetEncryptedChatKeyAsync(Guid requestorId, Guid otherUserId);
    }
}
