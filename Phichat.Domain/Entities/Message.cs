using Phichat.Domain.Entities;

public class Message
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }

    /// <summary>The other user of a private chat; null for group messages.</summary>
    public Guid? ReceiverId { get; set; }

    /// <summary>The group of a group message; null in private chats.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// Set for group service messages written by the server ("X added Y"): JSON with the event,
    /// its actor and targets. Such messages have no encrypted content.
    /// </summary>
    public string? SystemEvent { get; set; }
    public string EncryptedContent { get; set; } = default!;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }


    public User Sender { get; set; } = default!;
    public User? Receiver { get; set; }

    public string? FileUrl { get; set; }

    public bool IsRead { get; set; } = false;

    public Guid? ReplyToMessageId { get; set; }
    public Message? ReplyToMessage { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }

    public Guid? ForwardedFromMessageId { get; set; }
    public Guid? ForwardedFromSenderId { get; set; }



}
