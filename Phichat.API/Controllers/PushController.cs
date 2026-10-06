using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Settings;
using Phichat.Application.Interfaces;

namespace Phichat.API.Controllers;

/// <summary>Web Push subscriptions of this device (notifications while the app is closed).</summary>
[ApiController]
[Route("api/push")]
[Authorize]
public class PushController : ControllerBase
{
    private readonly IPushSubscriptionService _subscriptions;

    public PushController(IPushSubscriptionService subscriptions)
    {
        _subscriptions = subscriptions;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>The VAPID public key for <c>PushManager.subscribe</c>.</summary>
    [HttpGet("key")]
    public IActionResult GetKey()
    {
        return Ok(new { publicKey = _subscriptions.PublicKey });
    }

    [HttpPost("subscriptions")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionRequest request)
    {
        // Tied to the session so that ending it (or logging out) stops its notifications.
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.Sid), out var sessionId))
            throw new BadRequestException("session_unknown", "Sign in again to enable notifications.");

        await _subscriptions.SubscribeAsync(CurrentUserId, sessionId, request);
        return NoContent();
    }

    [HttpDelete("subscriptions")]
    public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeRequest request)
    {
        await _subscriptions.UnsubscribeAsync(CurrentUserId, request.Endpoint);
        return NoContent();
    }
}
