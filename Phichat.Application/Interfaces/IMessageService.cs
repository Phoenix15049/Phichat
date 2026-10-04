using Phichat.Application.DTOs.Message;

public interface IMessageService
{
    Task<Message> SendMessageAsync(Guid senderId, SendMessageRequest request);
    Task<Message> SendMessageWithFileAsync(Guid senderId, SendMessageWithFileRequest request, string uploadRootPath);

    Task<List<ReceivedMessageResponse>> GetReceivedMessagesAsync(Guid receiverId);
    Task<MessageReadResult> MarkAsReadAsync(Guid messageId, Guid readerId);
    Task<List<ReceivedMessageResponse>> GetConversationAsync(Guid currentUserId, Guid otherUserId);
    Task<List<ConversationDto>> GetConversationsAsync(Guid currentUserId);
    Task<PagedMessagesResponse> GetConversationPageAsync(Guid me, Guid other, Guid? beforeId, int pageSize);

    /// <summary>Single message for reply previews; only visible to its participants.</summary>
    Task<MessageBriefDto> GetBriefAsync(Guid userId, Guid messageId);

    Task<ReceivedMessageResponse> EditMessageAsync(Guid userId, Guid messageId, string encryptedText);
    Task DeleteMessageAsync(Guid userId, Guid messageId, string scope);
    Task<(Guid SenderId, Guid ReceiverId)?> GetPeerIdsForMessageAsync(Guid messageId);

    /// <summary>Adds the reaction and returns the new count for that emoji.</summary>
    Task<int> AddReactionAsync(Guid userId, Guid messageId, string emoji);

    /// <summary>Removes the reaction and returns the new count for that emoji.</summary>
    Task<int> RemoveReactionAsync(Guid userId, Guid messageId, string emoji);
}
