using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Phichat.API.Hubs;
using Phichat.API.Security;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Interfaces;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    public const long MaxFileBytes = 50_000_000;

    private readonly IMessageService _messageService;
    private readonly IGroupService _groups;
    private readonly IHubContext<ChatHub> _hub;
    private readonly IPushNotifier _push;

    public MessagesController(IMessageService messageService, IGroupService groups, IHubContext<ChatHub> hub, IPushNotifier push)
    {
        _messageService = messageService;
        _groups = groups;
        _hub = hub;
        _push = push;
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

        var payload = new
        {
            clientId,
            id = saved.Id,
            senderId = saved.SenderId,
            receiverId = saved.ReceiverId,
            groupId = saved.GroupId,
            encryptedContent = saved.EncryptedContent,
            fileUrl = saved.FileUrl,
            sentAt = saved.SentAt,
            replyToMessageId = saved.ReplyToMessageId,
            forwardedFromMessageId = saved.ForwardedFromMessageId,
            forwardedFromSenderId = saved.ForwardedFromSenderId
        };

        if (saved.GroupId is { } groupId)
        {
            var others = (await _groups.GetMemberIdsAsync(groupId)).Where(id => id != userId).ToList();
            await _hub.Clients.Users(ToUserIds(others)).SendAsync("ReceiveMessage", payload);
            foreach (var member in others) _push.NewMessage(member, userId, chatId: groupId);
        }
        else
        {
            await _hub.Clients.User(request.ReceiverId.ToString()).SendAsync("ReceiveMessage", payload);
            _push.NewMessage(request.ReceiverId, userId, chatId: userId);
        }

        await _hub.Clients.User(userId.ToString()).SendAsync("Delivered", new
        {
            clientId,
            messageId = saved.Id,
            groupId = saved.GroupId,
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
        var result = await _messageService.GetConversationPageAsync(CurrentUserId, userId, ParseAnchor(beforeId), pageSize);
        return Ok(result);
    }

    [HttpGet("group/{groupId:guid}/paged")]
    public async Task<IActionResult> GetGroupPaged(Guid groupId, [FromQuery] string? beforeId = null, [FromQuery] int pageSize = 50)
    {
        var result = await _messageService.GetGroupPageAsync(CurrentUserId, groupId, ParseAnchor(beforeId), pageSize);
        return Ok(result);
    }

    private static Guid? ParseAnchor(string? beforeId) =>
        !string.IsNullOrWhiteSpace(beforeId) && Guid.TryParse(beforeId, out var g) ? g : null;

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromBody] EditMessageRequest dto)
    {
        var res = await _messageService.EditMessageAsync(CurrentUserId, id, dto.EncryptedText);

        var audience = await _messageService.GetAudienceAsync(id);
        if (audience != null)
        {
            await _hub.Clients.Users(ToUserIds(audience.UserIds)).SendAsync("MessageEdited", new
            {
                messageId = id,
                groupId = audience.GroupId,
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
            var audience = await _messageService.GetAudienceAsync(id);
            if (audience != null)
            {
                await _hub.Clients.Users(ToUserIds(audience.UserIds)).SendAsync("MessageDeleted", new
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

    [HttpGet("pinned/{peerId:guid}")]
    public async Task<IActionResult> GetPinned(Guid peerId)
    {
        return Ok(await _messageService.GetPinnedAsync(CurrentUserId, peerId));
    }

    [HttpGet("group/{groupId:guid}/pinned")]
    public async Task<IActionResult> GetGroupPinned(Guid groupId)
    {
        return Ok(await _messageService.GetGroupPinnedAsync(CurrentUserId, groupId));
    }

    [HttpPost("{id:guid}/pin")]
    public async Task<IActionResult> Pin(Guid id)
    {
        var audience = await _messageService.PinAsync(CurrentUserId, id);
        await NotifyPinsChangedAsync(id, audience, pinned: true);
        return NoContent();
    }

    [HttpDelete("{id:guid}/pin")]
    public async Task<IActionResult> Unpin(Guid id)
    {
        var audience = await _messageService.UnpinAsync(CurrentUserId, id);
        await NotifyPinsChangedAsync(id, audience, pinned: false);
        return NoContent();
    }

    private Task NotifyPinsChangedAsync(Guid messageId, MessageAudience audience, bool pinned) =>
        _hub.Clients.Users(ToUserIds(audience.UserIds)).SendAsync("PinsChanged", new
        {
            messageId,
            pinned,
            by = CurrentUserId,
            senderId = audience.SenderId,
            receiverId = audience.ReceiverId,
            groupId = audience.GroupId
        });

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
        var audience = await _messageService.GetAudienceAsync(messageId);
        if (audience == null) return;

        await _hub.Clients.Users(ToUserIds(audience.UserIds)).SendAsync("ReactionUpdated", new
        {
            messageId,
            emoji,
            count,
            userId,
            action
        });
    }

    private static List<string> ToUserIds(IEnumerable<Guid> ids) =>
        ids.Select(id => id.ToString()).ToList();
}
