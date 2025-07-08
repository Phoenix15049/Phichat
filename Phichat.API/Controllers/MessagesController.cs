using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Interfaces;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;

    public MessagesController(IMessageService messageService)
    {
        _messageService = messageService;
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage(SendMessageRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _messageService.SendMessageAsync(userId, request);
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> GetMyMessages()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var messages = await _messageService.GetReceivedMessagesAsync(userId);
        return Ok(messages);
    }
}
