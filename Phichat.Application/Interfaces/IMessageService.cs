using Phichat.Application.DTOs.Message;
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


}
