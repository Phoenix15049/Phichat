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

    public async Task SendMessage(Guid receiverId, string encryptedText)
    {
        var senderIdStr = Context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        if (!Guid.TryParse(senderIdStr, out var senderId))
            return;

        var request = new SendMessageRequest
        {
            ReceiverId = receiverId,
            EncryptedText = encryptedText
        };

        await _messageService.SendMessageAsync(senderId, request);

        if (OnlineUsers.TryGetValue(receiverId, out var receiverConnectionId))
        {
            await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", new
            {
                SenderId = senderId,
                EncryptedText = encryptedText,
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

        if (OnlineUsers.TryGetValue(request.ReceiverId, out var connId))
        {
            await Clients.Client(connId).SendAsync("ReceiveMessage", new
            {
                SenderId = senderId,
                EncryptedText = request.EncryptedText,
                FileUrl = "[دریافت فایل]",
                SentAt = DateTime.UtcNow
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
