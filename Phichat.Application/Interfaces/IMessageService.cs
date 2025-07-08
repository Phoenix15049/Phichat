using Phichat.Application.DTOs.Message;

public interface IMessageService
{
    Task SendMessageAsync(Guid senderId, SendMessageRequest request);
    Task<List<ReceivedMessageResponse>> GetReceivedMessagesAsync(Guid receiverId);
    Task SendMessageWithFileAsync(Guid senderId, SendMessageWithFileRequest request, string uploadRootPath);

}
