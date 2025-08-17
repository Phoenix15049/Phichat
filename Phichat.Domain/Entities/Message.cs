using Phichat.Domain.Entities;

public class Message
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public string EncryptedContent { get; set; } = default!;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }


    public User Sender { get; set; } = default!;
    public User Receiver { get; set; } = default!;

    public string? FileUrl { get; set; }

    public bool IsRead { get; set; } = false;

}
