namespace Phichat.Application.DTOs.Message;

public class MessageBriefDto
{
    public Guid MessageId { get; set; }
    public Guid SenderId { get; set; }
    public Guid? ReceiverId { get; set; }
    public Guid? GroupId { get; set; }
    public string EncryptedContent { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public DateTime SentAt { get; set; }
    public Guid? ReplyToMessageId { get; set; }
}
