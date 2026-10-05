using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Phichat.API.Hubs;
using Phichat.Application.DTOs.Keys;
using Phichat.Application.Interfaces;
using System.Security.Claims;

namespace Phichat.API.Controllers;

/// <summary>End-to-end encryption identity keys.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KeysController : ControllerBase
{
    private readonly IIdentityKeyService _keys;
    private readonly IUserService _users;
    private readonly IHubContext<ChatHub> _hub;

    public KeysController(IIdentityKeyService keys, IUserService users, IHubContext<ChatHub> hub)
    {
        _keys = keys;
        _users = users;
        _hub = hub;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>The caller's active key and its encrypted backup (204 when none is published yet).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMine()
    {
        var key = await _keys.GetMyKeyAsync(CurrentUserId);
        return key == null ? NoContent() : Ok(key);
    }

    [HttpPost("me")]
    public async Task<IActionResult> Create([FromBody] PublishIdentityKeyRequest request)
    {
        var key = await _keys.CreateAsync(CurrentUserId, request);
        await NotifyKeyChangedAsync(key.KeyId);
        return Ok(key);
    }

    /// <summary>Replaces the active key (lost recovery passphrase). Peers are told to refresh it.</summary>
    [HttpPut("me")]
    public async Task<IActionResult> Replace([FromBody] PublishIdentityKeyRequest request)
    {
        var key = await _keys.ReplaceAsync(CurrentUserId, request);
        await NotifyKeyChangedAsync(key.KeyId);
        return Ok(key);
    }

    [HttpPut("me/backup")]
    public async Task<IActionResult> UpdateBackup([FromBody] UpdateKeyBackupRequest request)
    {
        await _keys.UpdateBackupAsync(CurrentUserId, request);
        return NoContent();
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetActive(Guid userId)
    {
        return Ok(await _keys.GetActiveKeyAsync(userId));
    }

    [HttpGet("{userId:guid}/{keyId}")]
    public async Task<IActionResult> GetById(Guid userId, string keyId)
    {
        return Ok(await _keys.GetKeyAsync(userId, keyId));
    }

    private async Task NotifyKeyChangedAsync(string keyId)
    {
        var userId = CurrentUserId;
        var related = await _users.GetRelatedUserIdsAsync(userId);

        // The user's other devices hold the old key and must restore the new one.
        var recipients = related.Append(userId).Select(id => id.ToString()).Distinct().ToList();

        await _hub.Clients.Users(recipients).SendAsync("IdentityKeyChanged", new { userId, keyId });
    }
}
