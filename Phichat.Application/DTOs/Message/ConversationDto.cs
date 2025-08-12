namespace Phichat.Application.DTOs.Message;

public class ConversationDto
{
    public Guid PeerId { get; set; }
    public string PeerUsername { get; set; } = default!;
    public string? PeerDisplayName { get; set; }
    public string? PeerAvatarUrl { get; set; }

    public string? LastEncryptedContent { get; set; }
    public string? LastFileUrl { get; set; }
    public DateTime LastSentAt { get; set; }

    public int UnreadCount { get; set; }
}
