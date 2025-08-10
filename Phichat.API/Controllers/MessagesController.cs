using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    

    public MessagesController(AppDbContext context, IMessageService messageService)
    {
        _context = context;
        _messageService = messageService;
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
}
