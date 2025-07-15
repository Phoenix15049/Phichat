using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phichat.Application.DTOs.ChatKey;
using Phichat.Application.Interfaces;
using System.Security.Claims;

namespace Phichat.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KeysController : ControllerBase
{
    private readonly IChatKeyService _chatKeyService;

    public KeysController(IChatKeyService chatKeyService)
    {
        _chatKeyService = chatKeyService;
    }

    [HttpPost]
    public async Task<IActionResult> PostKey(ChatKeyDto dto)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var keyBytes = Convert.FromBase64String(dto.EncryptedKeyBase64);
        await _chatKeyService.StoreChatKeyAsync(userId, dto.ReceiverId, keyBytes);
        return Ok();
    }

    [HttpGet("{otherUserId}")]
    public async Task<IActionResult> GetKey(Guid otherUserId)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var key = await _chatKeyService.GetEncryptedChatKeyAsync(userId, otherUserId);
        if (key == null) return NotFound();
        return Ok(Convert.ToBase64String(key));
    }
}

