namespace Phichat.Domain.Entities;

/// <summary>A chat the user muted: no notifications for it on any device.</summary>
public class ChatMute
{
    public Guid UserId { get; set; }

    /// <summary>The other user of a private chat (or a group id).</summary>
    public Guid ChatId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
