using Microsoft.AspNetCore.SignalR;
using Phichat.API.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Phichat.API.Security;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.User;
using Phichat.Application.Interfaces;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private const long MaxAvatarBytes = 10_000_000;

    private readonly IUserService _userService;
    private readonly IBlockService _blocks;
    private readonly IHubContext<ChatHub> _hub;
    private readonly PresenceTracker _presence;

    public UsersController(IUserService userService, IBlockService blocks, IHubContext<ChatHub> hub, PresenceTracker presence)
    {
        _userService = userService;
        _blocks = blocks;
        _hub = hub;
        _presence = presence;
    }

    // ---------- blocking ----------

    [HttpGet("blocked")]
    public async Task<IActionResult> GetBlocked()
    {
        return Ok(await _blocks.GetBlockedAsync(CurrentUserId));
    }

    /// <summary>
    /// Blocks a user. Both sides immediately see each other as offline; the blocked user is not told.
    /// </summary>
    [HttpPost("{id:guid}/block")]
    public async Task<IActionResult> Block(Guid id)
    {
        var me = CurrentUserId;
        await _blocks.BlockAsync(me, id);

        var at = DateTime.UtcNow.ToString("o");
        await _hub.Clients.User(id.ToString()).SendAsync("UserOffline", me.ToString(), at);
        await _hub.Clients.User(me.ToString()).SendAsync("UserOffline", id.ToString(), at);
        await _hub.Clients.User(me.ToString()).SendAsync("BlockListChanged", new { userId = id, blocked = true });
        return NoContent();
    }

    [HttpDelete("{id:guid}/block")]
    public async Task<IActionResult> Unblock(Guid id)
    {
        var me = CurrentUserId;
        await _blocks.UnblockAsync(me, id);

        // Presence resumes right away when they are still related and not blocked the other way.
        if (!await _blocks.IsBlockedEitherWayAsync(me, id))
        {
            var at = DateTime.UtcNow.ToString("o");
            if (_presence.IsOnline(id)) await _hub.Clients.User(me.ToString()).SendAsync("UserOnline", id.ToString(), at);
            if (_presence.IsOnline(me)) await _hub.Clients.User(id.ToString()).SendAsync("UserOnline", me.ToString(), at);
        }

        await _hub.Clients.User(me.ToString()).SendAsync("BlockListChanged", new { userId = id, blocked = false });
        return NoContent();
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> Get(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null) return NotFound();
        if (await _blocks.IsBlockedEitherWayAsync(CurrentUserId, id)) user.LastSeenUtc = null;
        return Ok(user);
    }

    /// <summary>Public profile. The phone number is private and only returned by <c>/me</c>.</summary>
    [HttpGet("by-username/{username}")]
    public async Task<IActionResult> GetByUsername(string username)
    {
        var profile = await _userService.GetProfileByUsernameAsync(username);
        if (profile == null) return NotFound();
        if (await _blocks.IsBlockedEitherWayAsync(CurrentUserId, profile.Id)) profile.LastSeenUtc = null;
        return Ok(profile);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var profile = await _userService.GetMyProfileAsync(CurrentUserId);
        if (profile == null) return NotFound();
        return Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        await _userService.UpdateProfileAsync(CurrentUserId, req);
        return NoContent();
    }

    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxAvatarBytes)]
    [RequestSizeLimit(MaxAvatarBytes)]
    public async Task<IActionResult> UploadAvatar([FromForm] AvatarUploadRequest model)
    {
        var file = model.File;
        if (file == null || file.Length == 0)
            throw new BadRequestException("file_required", "No file.");

        // Trust the bytes, not the client's file name or content type.
        var ext = await ImageSignature.DetectExtensionAsync(file)
            ?? throw new BadRequestException("invalid_image", "Only JPEG, PNG, WebP or GIF images are allowed.");

        var userId = CurrentUserId;

        // wwwroot/uploads/avatars/{userId}/yyyyMMddHHmmssfff.ext
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars", userId.ToString());
        Directory.CreateDirectory(dir);

        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
            await file.CopyToAsync(stream);

        var relativeUrl = $"/uploads/avatars/{userId}/{fileName}";
        return Ok(new { url = relativeUrl });
    }

    [AllowAnonymous]
    [HttpGet("check-username")]
    [EnableRateLimiting(RateLimitPolicies.Lookup)]
    public async Task<IActionResult> CheckUsername([FromQuery] string u)
    {
        if (string.IsNullOrWhiteSpace(u) || u.Length > 64) return BadRequest();
        return Ok(new { available = await _userService.IsUsernameAvailableAsync(u) });
    }
}
