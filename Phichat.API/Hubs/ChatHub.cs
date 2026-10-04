using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;
using System.Security.Claims;

namespace Phichat.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    /// <summary>Hub endpoint. Kept under /hubs so it never collides with client-side routes such as /chat.</summary>
    public const string Path = "/hubs/chat";

    private static readonly Dictionary<Guid, string> OnlineUsers = new();

    private readonly IMessageService _messageService;
    private readonly IUserService _userService; // NEW
    private readonly ILogger<ChatHub> _logger;
    public ChatHub(IMessageService messageService, IUserService userService, ILogger<ChatHub> logger) // NEW
    {
        _messageService = messageService;
        _userService = userService;
        _logger = logger;
    }

    public class SimpleMessageDto
    {
        public Guid ReceiverId { get; set; }
        public string EncryptedText { get; set; } = string.Empty;
        public string? ClientId { get; set; }
        public Guid? ReplyToMessageId { get; set; }
        public Guid? ForwardedFromMessageId { get; set; }  // NEW
    }


    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (Guid.TryParse(userId, out var id))
        {
            OnlineUsers[id] = Context.ConnectionId;

            var now = DateTime.UtcNow;
            await _userService.UpdateLastSeenAsync(id, now);

            await Clients.All.SendAsync("UserOnline", id.ToString(), now.ToString("o"));
            var snapshot = OnlineUsers.Keys.Select(g => g.ToString()).ToArray();
            await Clients.Caller.SendAsync(
                "OnlineSnapshot",
                OnlineUsers.Keys.Select(k => k.ToString()).ToArray()
            );


            await Clients.All.SendAsync("UserLastSeen", id.ToString(), now.ToString("o"));
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (Guid.TryParse(userId, out var id))
        {
            OnlineUsers.Remove(id);

            var now = DateTime.UtcNow;
            await _userService.UpdateLastSeenAsync(id, now);

            await Clients.All.SendAsync("UserOffline", id.ToString(), now.ToString("o"));

            await Clients.All.SendAsync("UserLastSeen", id.ToString(), now.ToString("o"));
        }
        await base.OnDisconnectedAsync(exception);
    }


    public async Task SendMessage(SimpleMessageDto dto)
    {
        var senderIdStr = Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (!Guid.TryParse(senderIdStr, out var senderId))
            return;



        var request = new SendMessageRequest
        {
            ReceiverId = dto.ReceiverId,
            EncryptedText = dto.EncryptedText,
            ReplyToMessageId = dto.ReplyToMessageId,
            ForwardedFromMessageId = dto.ForwardedFromMessageId
        };

        var latest = await _messageService.SendMessageAsync(senderId, request);

        // send to receiver if online
        if (OnlineUsers.TryGetValue(dto.ReceiverId, out var receiverConn))
        {
            await Clients.Client(receiverConn).SendAsync("ReceiveMessage", new
            {
                MessageId = latest.Id,
                SenderId = senderId,
                EncryptedText = latest.EncryptedContent,
                FileUrl = latest.FileUrl,
                SentAt = latest.SentAt,
                ReplyToMessageId = latest.ReplyToMessageId,
                ForwardedFromMessageId = latest.ForwardedFromMessageId,
                ForwardedFromSenderId = latest.ForwardedFromSenderId
            });
        }

        if (OnlineUsers.TryGetValue(senderId, out var sc))
        {
            await Clients.Client(sc).SendAsync("Delivered", new
            {
                MessageId = latest.Id,
                ReceiverId = dto.ReceiverId,
                ClientId = dto.ClientId,            // <- echo back
                SentAt = latest.SentAt,
                deliveredAtUtc = latest.DeliveredAtUtc,
                FileUrl = latest.FileUrl,
                ForwardedFromMessageId = latest.ForwardedFromMessageId,
                ForwardedFromSenderId = latest.ForwardedFromSenderId
            });
        }
    }




    public async Task MarkMessageAsRead(Guid messageId)
    {
        var userIdStr = Context.UserIdentifier;
        if (!Guid.TryParse(userIdStr, out var userId))
            return;

        var result = await _messageService.MarkAsReadAsync(messageId, userId);

        if (!result.Success || result.SenderId == null)
            return;

        if (OnlineUsers.TryGetValue(result.SenderId.Value, out var senderConn))
        {
            await Clients.Client(senderConn).SendAsync("MessageRead", new
            {
                MessageId = messageId,
                ReaderId = userId,
                readAtUtc = result.ReadAtUtc
            });
        }
    }

    public async Task StartTyping(Guid receiverId)
    {
        var me = Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (!Guid.TryParse(me, out var senderId)) return;

        if (OnlineUsers.TryGetValue(receiverId, out var recvConn))
        {
            await Clients.Client(recvConn).SendAsync("UserTyping", new
            {
                SenderId = senderId.ToString(),
                At = DateTime.UtcNow.ToString("o")
            });
        }
    }

    public async Task StopTyping(Guid receiverId)
    {
        var me = Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (!Guid.TryParse(me, out var senderId)) return;

        if (OnlineUsers.TryGetValue(receiverId, out var recvConn))
        {
            await Clients.Client(recvConn).SendAsync("UserStoppedTyping", new
            {
                SenderId = senderId.ToString(),
                At = DateTime.UtcNow.ToString("o")
            });
        }
    }

    public Task<string[]> GetOnlineUsers()
    {
        var arr = OnlineUsers.Keys.Select(x => x.ToString()).ToArray();
        return Task.FromResult(arr);
    }

}
