using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;

namespace Phichat.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private static readonly Dictionary<Guid, string> OnlineUsers = new();
    private readonly IMessageService _messageService;

    public ChatHub(IMessageService messageService)
    {
        _messageService = messageService;
    }

    public class SimpleMessageDto
    {
        public Guid ReceiverId { get; set; }
        public string EncryptedText { get; set; } = string.Empty;
    }

    public override Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (Guid.TryParse(userId, out var id))
        {
            OnlineUsers[id] = Context.ConnectionId;
        }

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (Guid.TryParse(userId, out var id))
        {
            OnlineUsers.Remove(id);
        }

        return base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(SimpleMessageDto dto)
    {
        var senderIdStr = Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        if (!Guid.TryParse(senderIdStr, out var senderId))
            return;

        var request = new SendMessageRequest
        {
            ReceiverId = dto.ReceiverId,
            EncryptedText = dto.EncryptedText
        };

        await _messageService.SendMessageAsync(senderId, request);

        if (OnlineUsers.TryGetValue(dto.ReceiverId, out var receiverConnectionId))
        {
            await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", new
            {
                SenderId = senderId,
                EncryptedText = dto.EncryptedText,
                SentAt = DateTime.UtcNow
            });
        }
    }




    public async Task SendMessageWithFile(SendMessageViaHubRequest request)
    {
        var senderIdStr = Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (!Guid.TryParse(senderIdStr, out var senderId))
            return;

        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

        await _messageService.SendMessageFromHubAsync(senderId, request, uploadPath);

        // فایل در MessageService ذخیره شده، باید url رو از دیتابیس بگیریم
        var latest = await _messageService.GetLastMessageBetweenAsync(senderId, request.ReceiverId);

        if (OnlineUsers.TryGetValue(request.ReceiverId, out var connId))
        {
            await Clients.Client(connId).SendAsync("ReceiveMessage", new
            {
                SenderId = senderId,
                EncryptedText = request.EncryptedText,
                FileUrl = latest?.FileUrl, // 👈 لینک واقعی فایل
                SentAt = latest?.SentAt ?? DateTime.UtcNow
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
                ReaderId = userId
            });
        }
    }





}
