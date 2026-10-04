namespace Phichat.Application.DTOs.Message;

public class SendMessageRequest
{
    public Guid ReceiverId { get; set; }
    public string EncryptedText { get; set; } = string.Empty;
    public Guid? ReplyToMessageId { get; set; }

    /// <summary>
    /// When set, the message is a forward: its attachment is copied from that message
    /// (which the sender must be able to see), never from client input.
    /// </summary>
    public Guid? ForwardedFromMessageId { get; set; }
}
