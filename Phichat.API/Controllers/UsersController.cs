using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Phichat.API.Security;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.User;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private const long MaxAvatarBytes = 10_000_000;

    private readonly IUserService _userService;
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context, IUserService userService)
    {
        _context = context;
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
        var u = await _context.Users
            .Where(x => x.Username == username)
            .Select(x => new UserProfileDto
            {
                Id = x.Id,
                Username = x.Username,
                DisplayName = x.DisplayName,
                AvatarUrl = x.AvatarUrl,
                Bio = x.Bio,
                LastSeenUtc = x.LastSeenUtc
            })
            .FirstOrDefaultAsync();

        if (u == null) return NotFound();
        return Ok(u);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userId = CurrentUserId;
        var u = await _context.Users
            .Where(x => x.Id == userId)
            .Select(x => new UserProfileDto
            {
                Id = x.Id,
                Username = x.Username,
                DisplayName = x.DisplayName,
                AvatarUrl = x.AvatarUrl,
                Bio = x.Bio,
                LastSeenUtc = x.LastSeenUtc,
                PhoneNumber = x.PhoneNumber
            })
            .FirstOrDefaultAsync();

        if (u == null) return NotFound();
        return Ok(u);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var userId = CurrentUserId;
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        user.DisplayName = NullIfEmpty(req.DisplayName);
        user.AvatarUrl = NormalizeAvatarUrl(req.AvatarUrl, userId);
        user.Bio = NullIfEmpty(req.Bio);

        await _context.SaveChangesAsync();
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
        var exists = await _context.Users.AnyAsync(x => x.Username == u);
        return Ok(new { available = !exists });
    }

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// Only avatars uploaded by this user through <c>POST /api/users/avatar</c> are accepted, so a profile
    /// cannot point viewers' browsers at an arbitrary external URL. Stored as a server-relative path.
    /// </summary>
    private static string? NormalizeAvatarUrl(string? value, Guid userId)
    {
        var url = NullIfEmpty(value);
        if (url == null) return null;

        var path = Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https"
            ? absolute.AbsolutePath
            : url;

        var pattern = $@"^/uploads/avatars/{userId}/[0-9]{{17}}\.(jpe?g|png|webp|gif)$";
        if (!Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase))
            throw new BadRequestException("invalid_avatar_url", "Avatar must be an image uploaded to your profile.");

        return path;
    }
}
