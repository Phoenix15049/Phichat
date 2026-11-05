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
    /// <summary>Deletes a message; returns the attachment no other message uses any more, to remove from storage.</summary>
    Task<string?> DeleteMessageAsync(Guid userId, Guid messageId, string scope);

    /// <summary>
    /// Marks the private chat with <paramref name="peerId"/> as read up to a message (older unread
    /// ones included, even if they were never loaded). Returns that message's time, or null when nothing changed.
    /// </summary>
    Task<DateTime?> MarkChatReadAsync(Guid readerId, Guid peerId, Guid upToMessageId);

    /// <summary>Who must hear about changes to a message: both users of a private chat, or the group's members.</summary>
    Task<MessageAudience?> GetAudienceAsync(Guid messageId);

    /// <summary>
    /// Sets the user's reaction (one per user, like Telegram: another emoji replaces the previous one)
    /// and returns all reactions of the message.
    /// </summary>
    Task<ReactionSnapshot> AddReactionAsync(Guid userId, Guid messageId, string emoji);

    /// <summary>Removes the user's reaction and returns all reactions of the message.</summary>
    Task<ReactionSnapshot> RemoveReactionAsync(Guid userId, Guid messageId, string emoji);

    /// <summary>Pins a message in its conversation; returns who to notify.</summary>
    Task<MessageAudience> PinAsync(Guid userId, Guid messageId);

    Task<MessageAudience> UnpinAsync(Guid userId, Guid messageId);

    /// <summary>Pinned messages of the conversation with <paramref name="peerId"/>, newest pin first.</summary>
    Task<List<PinnedMessageDto>> GetPinnedAsync(Guid userId, Guid peerId);

    Task<List<PinnedMessageDto>> GetGroupPinnedAsync(Guid userId, Guid groupId);
}
