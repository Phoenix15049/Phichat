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

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> Get(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null) return NotFound();
        return Ok(user);
    }

    /// <summary>Public profile. The phone number is private and only returned by <c>/me</c>.</summary>
    [HttpGet("by-username/{username}")]
    public async Task<IActionResult> GetByUsername(string username)
    {
        var profile = await _userService.GetProfileByUsernameAsync(username);
        if (profile == null) return NotFound();
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
