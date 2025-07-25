﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;
using System.Security.Claims;

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



    [HttpGet]
    public async Task<IActionResult> GetMyMessages()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var messages = await _messageService.GetReceivedMessagesAsync(userId);
        return Ok(messages);
    }
}
