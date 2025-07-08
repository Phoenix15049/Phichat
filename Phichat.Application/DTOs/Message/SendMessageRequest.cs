namespace Phichat.Application.DTOs.Message;

public class SendMessageRequest
{
    public Guid ReceiverId { get; set; }
    public string PlainText { get; set; } = string.Empty;
}
