using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Phichat.API.Hubs;
using Phichat.Application.DTOs.Settings;
using Phichat.Application.Interfaces;

namespace Phichat.API.Controllers;

/// <summary>Account-wide settings shared by all devices: privacy and muted chats.</summary>
[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly IPrivacyService _privacy;
    private readonly IMuteService _mutes;
    private readonly IUserService _users;
    private readonly IHubContext<ChatHub> _hub;
    private readonly PresenceTracker _presence;

    public SettingsController(IPrivacyService privacy, IMuteService mutes, IUserService users, IHubContext<ChatHub> hub, PresenceTracker presence)
    {
        _privacy = privacy;
        _mutes = mutes;
        _users = users;
        _hub = hub;
        _presence = presence;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("privacy")]
    public async Task<IActionResult> GetPrivacy()
    {
        return Ok(await _privacy.GetAsync(CurrentUserId));
    }

    /// <summary>Saves privacy settings and updates who sees whose presence right away (it is mutual).</summary>
    [HttpPut("privacy")]
    public async Task<IActionResult> UpdatePrivacy([FromBody] PrivacySettingsDto settings)
    {
        var me = CurrentUserId;
        var before = (await _privacy.GetPresenceAudienceAsync(me)).ToHashSet();
        await _privacy.UpdateAsync(me, settings);
        var after = (await _privacy.GetPresenceAudienceAsync(me)).ToHashSet();

        foreach (var lost in before.Except(after))
        {
            await _hub.Clients.User(lost.ToString()).SendAsync("PresenceHidden", me.ToString());
            await _hub.Clients.User(me.ToString()).SendAsync("PresenceHidden", lost.ToString());
        }

        foreach (var gained in after.Except(before))
        {
            await SharePresenceAsync(subject: me, viewer: gained);
            await SharePresenceAsync(subject: gained, viewer: me);
        }

        return NoContent();
    }

    [HttpGet("mutes")]
    public async Task<IActionResult> GetMutes()
    {
        return Ok(await _mutes.GetMutedAsync(CurrentUserId));
    }

    [HttpPut("mutes/{chatId:guid}")]
    public async Task<IActionResult> Mute(Guid chatId)
    {
        await _mutes.MuteAsync(CurrentUserId, chatId);
        await _hub.Clients.User(CurrentUserId.ToString()).SendAsync("MutesChanged", new { chatId, muted = true });
        return NoContent();
    }

    [HttpDelete("mutes/{chatId:guid}")]
    public async Task<IActionResult> Unmute(Guid chatId)
    {
        await _mutes.UnmuteAsync(CurrentUserId, chatId);
        await _hub.Clients.User(CurrentUserId.ToString()).SendAsync("MutesChanged", new { chatId, muted = false });
        return NoContent();
    }

    private async Task SharePresenceAsync(Guid subject, Guid viewer)
    {
        var target = _hub.Clients.User(viewer.ToString());
        if (_presence.IsOnline(subject))
        {
            await target.SendAsync("UserOnline", subject.ToString(), DateTime.UtcNow.ToString("o"));
            return;
        }

        var user = await _users.GetUserByIdAsync(subject);
        if (user?.LastSeenUtc is { } lastSeen)
            await target.SendAsync("UserLastSeen", subject.ToString(), lastSeen.ToString("o"));
    }
}
