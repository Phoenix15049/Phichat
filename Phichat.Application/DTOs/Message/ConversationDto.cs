namespace Phichat.Application.DTOs.Message;

public class ConversationDto
{
    /// <summary>The other user, or the group id for a group (then <see cref="IsGroup"/> is set).</summary>
    public Guid PeerId { get; set; }
    public bool IsGroup { get; set; }
    public int MemberCount { get; set; }

    public string PeerUsername { get; set; } = default!;
    public string? PeerDisplayName { get; set; }
    public string? PeerAvatarUrl { get; set; }

    public Guid LastSenderId { get; set; }
    public string? LastEncryptedContent { get; set; }
    public string? LastFileUrl { get; set; }
    public string? LastSystemEvent { get; set; }
    public DateTime LastSentAt { get; set; }

    public int UnreadCount { get; set; }
}
