namespace Phichat.Application.DTOs.Message;

/// <summary>A pinned message of a conversation; the client decrypts the content for the banner.</summary>
public class PinnedMessageDto
{
    public Guid MessageId { get; set; }
    public Guid SenderId { get; set; }
    public string EncryptedContent { get; set; } = default!;
    public string? FileUrl { get; set; }
    public DateTime SentAt { get; set; }
    public Guid PinnedById { get; set; }
    public DateTime PinnedAtUtc { get; set; }
}
