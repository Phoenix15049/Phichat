using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Interfaces;
using System.Security.Claims;

namespace Phichat.API.Hubs;

/// <summary>
/// Real-time messaging. Events are addressed to users (<c>Clients.User</c>), so every open
/// connection of a user receives them; presence is shared only with related users whose
/// privacy settings allow it (see <see cref="IPrivacyService"/>).
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
    private readonly IPrivacyService _privacy;
    private readonly SessionConnections _sessions;
    private readonly IPushNotifier _push;
    private readonly IGroupService _groups;

    public ChatHub(
        IMessageService messageService,
        IUserService userService,
        PresenceTracker presence,
        IBlockService blocks,
        IPrivacyService privacy,
        SessionConnections sessions,
        IPushNotifier push,
        IGroupService groups)
    {
        _messageService = messageService;
        _userService = userService;
        _presence = presence;
        _blocks = blocks;
        _privacy = privacy;
        _sessions = sessions;
        _push = push;
        _groups = groups;
    }

    public class SimpleMessageDto
    {
        public Guid ReceiverId { get; set; }

        /// <summary>Set instead of <see cref="ReceiverId"/> for a group message.</summary>
        public Guid? GroupId { get; set; }
        public string EncryptedText { get; set; } = string.Empty;
        public string? ClientId { get; set; }
        public Guid? ReplyToMessageId { get; set; }
        public Guid? ForwardedFromMessageId { get; set; }
    }

    /// <summary>The authenticated user (NameIdentifier claim, which is also SignalR's user id).</summary>
    private Guid CurrentUserId => Guid.Parse(Context.UserIdentifier!);

    /// <summary>The sign-in session of this connection (absent in tokens issued before sessions were tracked).</summary>
    private Guid? CurrentSessionId =>
        Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.Sid), out var id) ? id : null;

    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId;
        var cameOnline = _presence.Connected(userId, Context.ConnectionId);

        if (CurrentSessionId is { } sessionId)
        {
            _sessions.Connected(sessionId);
            // Ending the session from another device reaches this connection through the group.
            await Groups.AddToGroupAsync(Context.ConnectionId, SessionConnections.Group(sessionId));
        }

        var related = await _privacy.GetPresenceAudienceAsync(userId);

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

        if (CurrentSessionId is { } sessionId)
            _sessions.Disconnected(sessionId);

        // Another tab or a fresh reconnect may still be open; only the last connection marks the user offline.
        if (_presence.Disconnected(userId, Context.ConnectionId))
        {
            var now = DateTime.UtcNow;
            await _userService.UpdateLastSeenAsync(userId, now);

            var related = await _privacy.GetPresenceAudienceAsync(userId);
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
            GroupId = dto.GroupId,
            EncryptedText = dto.EncryptedText,
            ReplyToMessageId = dto.ReplyToMessageId,
            ForwardedFromMessageId = dto.ForwardedFromMessageId
        };

        var saved = await _messageService.SendMessageAsync(senderId, request);

        var payload = new
        {
            MessageId = saved.Id,
            SenderId = senderId,
            GroupId = saved.GroupId,
            EncryptedText = saved.EncryptedContent,
            FileUrl = saved.FileUrl,
            SentAt = saved.SentAt,
            ReplyToMessageId = saved.ReplyToMessageId,
            ForwardedFromMessageId = saved.ForwardedFromMessageId,
            ForwardedFromSenderId = saved.ForwardedFromSenderId
        };

        if (saved.GroupId is { } groupId)
        {
            var others = (await _groups.GetMemberIdsAsync(groupId)).Where(id => id != senderId).ToList();
            await Clients.Users(ToUserIds(others)).SendAsync("ReceiveMessage", payload);
            foreach (var member in others) _push.NewMessage(member, senderId, chatId: groupId);

            await Clients.Caller.SendAsync("Delivered", new
            {
                MessageId = saved.Id,
                GroupId = groupId,
                ClientId = dto.ClientId,
                SentAt = saved.SentAt,
                deliveredAtUtc = saved.DeliveredAtUtc,
                FileUrl = saved.FileUrl,
                ForwardedFromMessageId = saved.ForwardedFromMessageId,
                ForwardedFromSenderId = saved.ForwardedFromSenderId
            });
            return;
        }

        await Clients.User(dto.ReceiverId.ToString()).SendAsync("ReceiveMessage", payload);

        _push.NewMessage(dto.ReceiverId, senderId, chatId: senderId);

        // A first message makes the two users related; share their presence right away
        // instead of waiting for the next connect.
        if (dto.ReceiverId != senderId && await _privacy.CanSeePresenceAsync(dto.ReceiverId, senderId))
        {
            var now = DateTime.UtcNow.ToString("o");
            await Clients.User(dto.ReceiverId.ToString()).SendAsync("UserOnline", senderId.ToString(), now);
            if (_presence.IsOnline(dto.ReceiverId))
                await Clients.Caller.SendAsync("UserOnline", dto.ReceiverId.ToString(), now);
        }

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

        // Still recorded (for unread counts), but the sender is told only when both share read receipts.
        if (!await _privacy.ReadReceiptsSharedAsync(userId, result.SenderId.Value))
            return;

        await Clients.User(result.SenderId.Value.ToString()).SendAsync("MessageRead", new
        {
            MessageId = messageId,
            ReaderId = userId,
            readAtUtc = result.ReadAtUtc
        });
    }

    /// <summary>Moves the caller's read position in a group; members (and the caller's other devices) are told.</summary>
    public async Task MarkGroupRead(Guid groupId, Guid messageId)
    {
        var userId = CurrentUserId;
        var readUpTo = await _messageService.MarkGroupReadAsync(groupId, userId, messageId);
        if (readUpTo == null) return;

        var members = await _groups.GetMemberIdsAsync(groupId);
        await Clients.Users(ToUserIds(members)).SendAsync("GroupRead", new
        {
            groupId,
            readerId = userId,
            readUpToUtc = readUpTo
        });
    }

    public Task StartGroupTyping(Guid groupId) => SendGroupTypingAsync(groupId, "UserTyping");

    public Task StopGroupTyping(Guid groupId) => SendGroupTypingAsync(groupId, "UserStoppedTyping");

    private async Task SendGroupTypingAsync(Guid groupId, string eventName)
    {
        var me = CurrentUserId;
        var members = await _groups.GetMemberIdsAsync(groupId);
        if (!members.Contains(me)) return;

        await Clients.Users(ToUserIds(members.Where(id => id != me))).SendAsync(eventName, new
        {
            SenderId = me.ToString(),
            GroupId = groupId.ToString(),
            At = DateTime.UtcNow.ToString("o")
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

    /// <summary>Online users among the caller's conversation partners and contacts who share presence with them.</summary>
    public async Task<string[]> GetOnlineUsers()
    {
        var related = await _privacy.GetPresenceAudienceAsync(CurrentUserId);
        return ToUserIds(_presence.OnlineAmong(related)).ToArray();
    }

    private static List<string> ToUserIds(IEnumerable<Guid> ids) =>
        ids.Select(id => id.ToString()).ToList();
}
