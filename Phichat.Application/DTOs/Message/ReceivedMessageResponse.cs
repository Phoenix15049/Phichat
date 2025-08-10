namespace Phichat.Application.DTOs.Message;

public class ReceivedMessageResponse
{
    public Guid MessageId { get; set; }
    public Guid SenderId { get; set; }
    public string EncryptedContent { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string? FileUrl { get; set; }
    public bool IsRead { get; set; }
}
