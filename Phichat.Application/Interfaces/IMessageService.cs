using Phichat.Application.DTOs.Message;

public interface IMessageService
{
    /// <summary>Sends to a private chat (<c>ReceiverId</c>) or a group (<c>GroupId</c>).</summary>
    Task<Message> SendMessageAsync(Guid senderId, SendMessageRequest request);
    Task<Message> SendMessageWithFileAsync(Guid senderId, SendMessageWithFileRequest request, string uploadRootPath);

    Task<MessageReadResult> MarkAsReadAsync(Guid messageId, Guid readerId);

    /// <summary>
    /// Moves the member's read position in the group up to <paramref name="messageId"/>.
    /// Returns the new position, or null when it did not move.
    /// </summary>
    Task<DateTime?> MarkGroupReadAsync(Guid groupId, Guid readerId, Guid messageId);

    /// <summary>Private chats and groups, newest activity first.</summary>
    Task<List<ConversationDto>> GetConversationsAsync(Guid currentUserId);
    Task<PagedMessagesResponse> GetConversationPageAsync(Guid me, Guid other, Guid? beforeId, int pageSize);
    Task<PagedMessagesResponse> GetGroupPageAsync(Guid me, Guid groupId, Guid? beforeId, int pageSize);

    /// <summary>Single message for reply previews; only visible to its participants.</summary>
    Task<MessageBriefDto> GetBriefAsync(Guid userId, Guid messageId);

    Task<ReceivedMessageResponse> EditMessageAsync(Guid userId, Guid messageId, string encryptedText);
    Task DeleteMessageAsync(Guid userId, Guid messageId, string scope);

    /// <summary>Who must hear about changes to a message: both users of a private chat, or the group's members.</summary>
    Task<MessageAudience?> GetAudienceAsync(Guid messageId);

    /// <summary>Adds the reaction and returns the new count for that emoji.</summary>
    Task<int> AddReactionAsync(Guid userId, Guid messageId, string emoji);

    /// <summary>Removes the reaction and returns the new count for that emoji.</summary>
    Task<int> RemoveReactionAsync(Guid userId, Guid messageId, string emoji);

    /// <summary>Pins a message in its conversation; returns who to notify.</summary>
    Task<MessageAudience> PinAsync(Guid userId, Guid messageId);

    Task<MessageAudience> UnpinAsync(Guid userId, Guid messageId);

    /// <summary>Pinned messages of the conversation with <paramref name="peerId"/>, newest pin first.</summary>
    Task<List<PinnedMessageDto>> GetPinnedAsync(Guid userId, Guid peerId);

    Task<List<PinnedMessageDto>> GetGroupPinnedAsync(Guid userId, Guid groupId);
}
