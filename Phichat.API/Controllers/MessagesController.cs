using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Phichat.API.Hubs;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;
using System.Security.Claims;

public class EncryptedFileUploadRequest
{
    public IFormFile File { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IMessageService _messageService;
    private readonly IHubContext<ChatHub> _hub;


    public MessagesController(AppDbContext context, IMessageService messageService, IHubContext<ChatHub> hub)
    {
        _context = context;
        _messageService = messageService;
        _hub = hub;
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage(SendMessageRequest request)
    {
        Console.WriteLine($"SendMessage invoked: to {request.ReceiverId}, text len {request.EncryptedText.Length}");
        
        
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _messageService.SendMessageAsync(userId, request);

        return Ok();
    }

    [HttpPost("with-file")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> SendMessageWithFile([FromForm] SendMessageWithFileRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

        await _messageService.SendMessageWithFileAsync(userId, request, uploadPath);

        return Ok();
    }


    [HttpGet("with/{userId:guid}")]
    public async Task<IActionResult> GetConversationWith(Guid userId)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var messages = await _messageService.GetConversationAsync(currentUserId, userId);
        return Ok(messages);
    }





    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadEncryptedFile([FromForm] EncryptedFileUploadRequest request)
    {
        var file = request.File;
        if (file == null || file.Length == 0)
            return BadRequest("فایل ارسال نشده یا خالی است.");

        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var fileUrl = $"/uploads/{fileName}";
        return Ok(new { url = fileUrl });
    }



    [HttpGet]
    public async Task<IActionResult> GetMyMessages()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var messages = await _messageService.GetReceivedMessagesAsync(userId);
        return Ok(messages);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var me = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var data = await _messageService.GetConversationsAsync(me);
        return Ok(data);
    }


    [Authorize]
    [HttpGet("with-paged/{userId:guid}")]
    public async Task<IActionResult> GetWithPaged(Guid userId, [FromQuery] string? beforeId = null, [FromQuery] int pageSize = 50)
    {
        var me = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        Guid? anchor = null;
        if (!string.IsNullOrWhiteSpace(beforeId) && Guid.TryParse(beforeId, out var g)) anchor = g;

        var result = await _messageService.GetConversationPageAsync(me, userId, anchor, pageSize);
        return Ok(result);
    }


    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromBody] EditMessageRequest dto)
    {
        var me = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var res = await _messageService.EditMessageAsync(me, id, dto.EncryptedText);

        var peers = await _messageService.GetPeerIdsForMessageAsync(id);
        if (peers != null)
        {
            var userIds = new List<string>
        {
            peers.Value.SenderId.ToString(),
            peers.Value.ReceiverId.ToString()
        };

            await _hub.Clients.Users(userIds).SendAsync("MessageEdited", new
            {
                messageId = id,
                encryptedContent = res.EncryptedContent,
                updatedAtUtc = res.UpdatedAtUtc
            });
        }

        return Ok(res);
    }



    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string scope = "me")
    {
        var me = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var peers = await _messageService.GetPeerIdsForMessageAsync(id);

        await _messageService.DeleteMessageAsync(me, id, scope);

        if (scope == "all" && peers != null)
        {
            var userIds = new List<string>
        {
            peers.Value.SenderId.ToString(),
            peers.Value.ReceiverId.ToString()
        };

            await _hub.Clients.Users(userIds).SendAsync("MessageDeleted", new
            {
                messageId = id,
                scope = "all"
            });
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

    public class ToggleReactionRequest { public string Emoji { get; set; } = ""; }

    [Authorize]
    [HttpPost("{id:guid}/reactions")]
    public async Task<IActionResult> AddReaction(Guid id, [FromBody] ToggleReactionRequest req)
    {
        var me = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _messageService.AddReactionAsync(me, id, req.Emoji);

        var peers = await _messageService.GetPeerIdsForMessageAsync(id);
        if (peers != null)
        {
            var userIds = new List<string> { peers.Value.SenderId.ToString(), peers.Value.ReceiverId.ToString() };

            // شمارش جدید این ایموجی
            var count = await _context.MessageReactions
                .CountAsync(r => r.MessageId == id && r.Emoji == req.Emoji);

            await _hub.Clients.Users(userIds).SendAsync("ReactionUpdated", new
            {
                messageId = id,
                emoji = req.Emoji,
                count,
                userId = me,
                action = "added"
            });
        }
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id:guid}/reactions")]
    public async Task<IActionResult> RemoveReaction(Guid id, [FromQuery] string emoji)
    {
        var me = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _messageService.RemoveReactionAsync(me, id, emoji);

        var peers = await _messageService.GetPeerIdsForMessageAsync(id);
        if (peers != null)
        {
            var userIds = new List<string> { peers.Value.SenderId.ToString(), peers.Value.ReceiverId.ToString() };
            var count = await _context.MessageReactions
                .CountAsync(r => r.MessageId == id && r.Emoji == emoji);

            await _hub.Clients.Users(userIds).SendAsync("ReactionUpdated", new
            {
                messageId = id,
                emoji,
                count,
                userId = me,
                action = "removed"
            });
        }
        return NoContent();
    }



}
