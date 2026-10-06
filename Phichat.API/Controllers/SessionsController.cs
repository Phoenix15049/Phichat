using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Phichat.API.Hubs;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.Interfaces;

namespace Phichat.API.Controllers;

/// <summary>Active sessions (signed-in devices). The current one ends with a normal logout.</summary>
[ApiController]
[Route("api/sessions")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessions;
    private readonly IHubContext<ChatHub> _hub;

    public SessionsController(ISessionService sessions, IHubContext<ChatHub> hub)
    {
        _sessions = sessions;
        _hub = hub;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private Guid? CurrentSessionId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.Sid), out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _sessions.GetSessionsAsync(CurrentUserId, CurrentSessionId));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> End(Guid id)
    {
        if (id == CurrentSessionId)
            throw new BadRequestException("current_session", "Use log out to end this session.");

        await _sessions.EndAsync(CurrentUserId, id);
        await NotifyEndedAsync(id);
        return NoContent();
    }

    /// <summary>Ends every session except this one.</summary>
    [HttpDelete("others")]
    public async Task<IActionResult> EndOthers()
    {
        var current = CurrentSessionId
            ?? throw new BadRequestException("session_unknown", "Sign in again to manage other sessions.");

        foreach (var id in await _sessions.EndOthersAsync(CurrentUserId, current))
            await NotifyEndedAsync(id);

        return NoContent();
    }

    /// <summary>Open pages of the ended session sign out (and forget their encryption key) at once.</summary>
    private Task NotifyEndedAsync(Guid sessionId) =>
        _hub.Clients.Group(SessionConnections.Group(sessionId)).SendAsync("SessionTerminated");
}
