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

    [HttpGet("{otherUserId}")]
    public async Task<IActionResult> GetKey(Guid otherUserId)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var key = await _chatKeyService.GetEncryptedChatKeyAsync(userId, otherUserId);
        if (key == null) return NotFound();
        return Ok(Convert.ToBase64String(key));
    }

    [HttpPost]
    public async Task<IActionResult> StoreKey([FromBody] ChatKeyDto dto)
    {
        if (dto.ReceiverId == Guid.Empty || string.IsNullOrWhiteSpace(dto.EncryptedKey))
            return BadRequest("Invalid payload");

        var senderId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var keyBytes = Convert.FromBase64String(dto.EncryptedKey);
        await _chatKeyService.StoreChatKeyAsync(senderId, dto.ReceiverId, keyBytes);

        return Ok();
    }
}
