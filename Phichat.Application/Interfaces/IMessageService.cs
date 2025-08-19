using Phichat.Application.DTOs.Message;

public interface IMessageService
{
    Task SendMessageAsync(Guid senderId, SendMessageRequest request);
    Task<List<ReceivedMessageResponse>> GetReceivedMessagesAsync(Guid receiverId);
    Task SendMessageWithFileAsync(Guid senderId, SendMessageWithFileRequest request, string uploadRootPath);
    Task SendMessageFromHubAsync(Guid senderId, SendMessageViaHubRequest request, string uploadRootPath);
    Task<MessageReadResult> MarkAsReadAsync(Guid messageId, Guid readerId);
    Task<Message?> GetLastMessageBetweenAsync(Guid senderId, Guid receiverId);
    Task<List<ReceivedMessageResponse>> GetConversationAsync(Guid currentUserId, Guid otherUserId);
    Task<List<ConversationDto>> GetConversationsAsync(Guid currentUserId);
    Task<PagedMessagesResponse> GetConversationPageAsync(Guid me, Guid other, Guid? beforeId, int pageSize);
    Task<ReceivedMessageResponse> EditMessageAsync(Guid userId, Guid messageId, string encryptedText);
    Task DeleteMessageAsync(Guid userId, Guid messageId, string scope);
    Task<(Guid SenderId, Guid ReceiverId)?> GetPeerIdsForMessageAsync(Guid messageId);


}
