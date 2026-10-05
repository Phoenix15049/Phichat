namespace Phichat.Domain.Entities;

/// <summary>A message pinned in its conversation (visible to both participants). The content stays encrypted.</summary>
public class PinnedMessage
{
    public Guid MessageId { get; set; }
    public Guid PinnedById { get; set; }
    public DateTime PinnedAtUtc { get; set; }

    public Message Message { get; set; } = default!;
}
