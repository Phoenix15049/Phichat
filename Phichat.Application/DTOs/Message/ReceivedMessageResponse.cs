namespace Phichat.Application.DTOs.Message;

public class ReceivedMessageResponse
{
    public Guid MessageId { get; set; }
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public string EncryptedContent { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public string? FileUrl { get; set; }
    public bool IsRead { get; set; }
    public Guid? ReplyToMessageId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }


}
