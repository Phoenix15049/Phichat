namespace Phichat.Application.DTOs.Message;

public class SendMessageRequest
{
    /// <summary>The other user of a private chat (empty for a group message).</summary>
    public Guid ReceiverId { get; set; }

    /// <summary>Set (instead of <see cref="ReceiverId"/>) for a group message.</summary>
    public Guid? GroupId { get; set; }

    public string EncryptedText { get; set; } = string.Empty;
    public Guid? ReplyToMessageId { get; set; }

    /// <summary>
    /// When set, the message is a forward: its attachment is copied from that message
    /// (which the sender must be able to see), never from client input.
    /// </summary>
    public Guid? ForwardedFromMessageId { get; set; }
}
