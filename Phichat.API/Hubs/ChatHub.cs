using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Interfaces;

namespace Phichat.API.Hubs;

/// <summary>
/// Real-time messaging. Events are addressed to users (<c>Clients.User</c>), so every open
/// connection of a user receives them; presence is shared only with related users.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    /// <summary>Hub endpoint. Kept under /hubs so it never collides with client-side routes such as /chat.</summary>
    public const string Path = "/hubs/chat";

    private readonly IMessageService _messageService;
    private readonly IUserService _userService;
    private readonly PresenceTracker _presence;
    private readonly IBlockService _blocks;

    public ChatHub(IMessageService messageService, IUserService userService, PresenceTracker presence, IBlockService blocks)
    {
        _messageService = messageService;
        _userService = userService;
        _presence = presence;
        _blocks = blocks;
    }

    public class SimpleMessageDto
    {
        public Guid ReceiverId { get; set; }
        public string EncryptedText { get; set; } = string.Empty;
        public string? ClientId { get; set; }
        public Guid? ReplyToMessageId { get; set; }
        public Guid? ForwardedFromMessageId { get; set; }
    }

    /// <summary>The authenticated user (NameIdentifier claim, which is also SignalR's user id).</summary>
    private Guid CurrentUserId => Guid.Parse(Context.UserIdentifier!);

    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId;
        var cameOnline = _presence.Connected(userId, Context.ConnectionId);
        var related = await _userService.GetRelatedUserIdsAsync(userId);

        if (cameOnline)
        {
            var now = DateTime.UtcNow;
            await _userService.UpdateLastSeenAsync(userId, now);

            var audience = Clients.Users(ToUserIds(related));
            await audience.SendAsync("UserOnline", userId.ToString(), now.ToString("o"));
            await audience.SendAsync("UserLastSeen", userId.ToString(), now.ToString("o"));
        }

        await Clients.Caller.SendAsync("OnlineSnapshot", ToUserIds(_presence.OnlineAmong(related)));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId;

        // Another tab or a fresh reconnect may still be open; only the last connection marks the user offline.
        if (_presence.Disconnected(userId, Context.ConnectionId))
        {
            var now = DateTime.UtcNow;
            await _userService.UpdateLastSeenAsync(userId, now);

            var related = await _userService.GetRelatedUserIdsAsync(userId);
            var audience = Clients.Users(ToUserIds(related));
            await audience.SendAsync("UserOffline", userId.ToString(), now.ToString("o"));
            await audience.SendAsync("UserLastSeen", userId.ToString(), now.ToString("o"));
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(SimpleMessageDto dto)
    {
        var senderId = CurrentUserId;

        var request = new SendMessageRequest
        {
            ReceiverId = dto.ReceiverId,
            EncryptedText = dto.EncryptedText,
            ReplyToMessageId = dto.ReplyToMessageId,
            ForwardedFromMessageId = dto.ForwardedFromMessageId
        };

        var saved = await _messageService.SendMessageAsync(senderId, request);

        await Clients.User(dto.ReceiverId.ToString()).SendAsync("ReceiveMessage", new
        {
            MessageId = saved.Id,
            SenderId = senderId,
            EncryptedText = saved.EncryptedContent,
            FileUrl = saved.FileUrl,
            SentAt = saved.SentAt,
            ReplyToMessageId = saved.ReplyToMessageId,
            ForwardedFromMessageId = saved.ForwardedFromMessageId,
            ForwardedFromSenderId = saved.ForwardedFromSenderId
        });

        // A first message makes the two users related; share their presence right away
        // instead of waiting for the next connect.
        var now = DateTime.UtcNow.ToString("o");
        await Clients.User(dto.ReceiverId.ToString()).SendAsync("UserOnline", senderId.ToString(), now);
        if (dto.ReceiverId != senderId && _presence.IsOnline(dto.ReceiverId))
            await Clients.Caller.SendAsync("UserOnline", dto.ReceiverId.ToString(), now);

        // The confirmation goes to the connection that sent the message; it matches it by ClientId.
        await Clients.Caller.SendAsync("Delivered", new
        {
            MessageId = saved.Id,
            ReceiverId = dto.ReceiverId,
            ClientId = dto.ClientId,
            SentAt = saved.SentAt,
            deliveredAtUtc = saved.DeliveredAtUtc,
            FileUrl = saved.FileUrl,
            ForwardedFromMessageId = saved.ForwardedFromMessageId,
            ForwardedFromSenderId = saved.ForwardedFromSenderId
        });
    }

    public async Task MarkMessageAsRead(Guid messageId)
    {
        var userId = CurrentUserId;
        var result = await _messageService.MarkAsReadAsync(messageId, userId);

        if (!result.Success || result.SenderId == null)
            return;

        await Clients.User(result.SenderId.Value.ToString()).SendAsync("MessageRead", new
        {
            MessageId = messageId,
            ReaderId = userId,
            readAtUtc = result.ReadAtUtc
        });
    }

    public async Task StartTyping(Guid receiverId)
    {
        // Blocked pairs do not see each other typing (silently dropped).
        if (await _blocks.IsBlockedEitherWayAsync(CurrentUserId, receiverId)) return;

        await Clients.User(receiverId.ToString()).SendAsync("UserTyping", new
        {
            SenderId = CurrentUserId.ToString(),
            At = DateTime.UtcNow.ToString("o")
        });
    }

    public async Task StopTyping(Guid receiverId)
    {
        if (await _blocks.IsBlockedEitherWayAsync(CurrentUserId, receiverId)) return;

        await Clients.User(receiverId.ToString()).SendAsync("UserStoppedTyping", new
        {
            SenderId = CurrentUserId.ToString(),
            At = DateTime.UtcNow.ToString("o")
        });
    }

    /// <summary>Online users among the caller's conversation partners and contacts.</summary>
    public async Task<string[]> GetOnlineUsers()
    {
        var related = await _userService.GetRelatedUserIdsAsync(CurrentUserId);
        return ToUserIds(_presence.OnlineAmong(related)).ToArray();
    }

    private static List<string> ToUserIds(IEnumerable<Guid> ids) =>
        ids.Select(id => id.ToString()).ToList();
}
