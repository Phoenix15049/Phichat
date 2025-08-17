namespace Phichat.Application.DTOs.Message;

public class SendMessageRequest
{
    public Guid ReceiverId { get; set; }
    public string EncryptedText { get; set; }
    public string? FileUrl { get; set; }
    public Guid? ReplyToMessageId { get; set; }

}
