using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Phichat.API.Hubs;
using Phichat.API.Security;
using Phichat.Application.DTOs.Message;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    public const long MaxFileBytes = 50_000_000;

    private readonly IMessageService _messageService;
    private readonly IHubContext<ChatHub> _hub;

    public MessagesController(IMessageService messageService, IHubContext<ChatHub> hub)
    {
        _messageService = messageService;
        _hub = hub;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("with-file")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(MaxFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes)]
    public async Task<IActionResult> SendMessageWithFile([FromForm] SendMessageWithFileRequest request, [FromForm] string? clientId)
    {
        var userId = CurrentUserId;
        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

        var saved = await _messageService.SendMessageWithFileAsync(userId, request, uploadPath);

        await _hub.Clients.User(request.ReceiverId.ToString()).SendAsync("ReceiveMessage", new
        {
            clientId,
            id = saved.Id,
            senderId = saved.SenderId,
            receiverId = saved.ReceiverId,
            encryptedContent = saved.EncryptedContent,
            fileUrl = saved.FileUrl,
            sentAt = saved.SentAt,
            replyToMessageId = saved.ReplyToMessageId,
            forwardedFromMessageId = saved.ForwardedFromMessageId,
            forwardedFromSenderId = saved.ForwardedFromSenderId
        });

        await _hub.Clients.User(userId.ToString()).SendAsync("Delivered", new
        {
            clientId,
            messageId = saved.Id,
            sentAt = saved.SentAt,
            deliveredAtUtc = saved.DeliveredAtUtc ?? DateTime.UtcNow,
            encryptedText = saved.EncryptedContent,
            fileUrl = saved.FileUrl
        });

        return Ok();
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var data = await _messageService.GetConversationsAsync(CurrentUserId);
        return Ok(data);
    }

    [HttpGet("with-paged/{userId:guid}")]
    public async Task<IActionResult> GetWithPaged(Guid userId, [FromQuery] string? beforeId = null, [FromQuery] int pageSize = 50)
    {
        Guid? anchor = null;
        if (!string.IsNullOrWhiteSpace(beforeId) && Guid.TryParse(beforeId, out var g)) anchor = g;

        var result = await _messageService.GetConversationPageAsync(CurrentUserId, userId, anchor, pageSize);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromBody] EditMessageRequest dto)
    {
        var res = await _messageService.EditMessageAsync(CurrentUserId, id, dto.EncryptedText);

        var peers = await _messageService.GetPeerIdsForMessageAsync(id);
        if (peers != null)
        {
            await _hub.Clients.Users(PeerUserIds(peers.Value)).SendAsync("MessageEdited", new
            {
                messageId = id,
                encryptedContent = res.EncryptedContent,
                updatedAtUtc = res.UpdatedAtUtc
            });
        }

        return Ok(res);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string scope = "me")
    {
        var me = CurrentUserId;

        await _messageService.DeleteMessageAsync(me, id, scope);

        if (scope == "all")
        {
            var peers = await _messageService.GetPeerIdsForMessageAsync(id);
            if (peers != null)
            {
                await _hub.Clients.Users(PeerUserIds(peers.Value)).SendAsync("MessageDeleted", new
                {
                    messageId = id,
                    scope = "all"
                });
            }
        }
        else
        {
            await _hub.Clients.User(me.ToString()).SendAsync("MessageDeleted", new
            {
                messageId = id,
                scope = "me"
            });
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/reactions")]
    public async Task<IActionResult> AddReaction(Guid id, [FromBody] ReactionRequest req)
    {
        var me = CurrentUserId;
        var count = await _messageService.AddReactionAsync(me, id, req.Emoji);
        await BroadcastReactionAsync(id, req.Emoji.Trim(), count, me, "added");
        return NoContent();
    }

    [HttpDelete("{id:guid}/reactions")]
    public async Task<IActionResult> RemoveReaction(Guid id, [FromQuery] string emoji)
    {
        var me = CurrentUserId;
        var count = await _messageService.RemoveReactionAsync(me, id, emoji);
        await BroadcastReactionAsync(id, (emoji ?? "").Trim(), count, me, "removed");
        return NoContent();
    }

    [HttpGet("{id:guid}/brief")]
    public async Task<IActionResult> GetBrief(Guid id)
    {
        var brief = await _messageService.GetBriefAsync(CurrentUserId, id);
        return Ok(brief);
    }

    private async Task BroadcastReactionAsync(Guid messageId, string emoji, int count, Guid userId, string action)
    {
        var peers = await _messageService.GetPeerIdsForMessageAsync(messageId);
        if (peers == null) return;

        await _hub.Clients.Users(PeerUserIds(peers.Value)).SendAsync("ReactionUpdated", new
        {
            messageId,
            emoji,
            count,
            userId,
            action
        });
    }

    private static List<string> PeerUserIds((Guid SenderId, Guid ReceiverId) peers) =>
        new() { peers.SenderId.ToString(), peers.ReceiverId.ToString() };
}
