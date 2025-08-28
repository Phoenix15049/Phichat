using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Phichat.Application.DTOs.User;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;
using Phichat.Infrastructure.Services;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    private readonly AppDbContext _context;
    public UsersController(AppDbContext context, IUserService userService)
    {
        _context = context;
        _userService = userService;
    }



    [Authorize]
    [HttpGet("list")]
    public async Task<IActionResult> GetUsers()
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var users = await _context.Users
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                LastSeenUtc = u.LastSeenUtc,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl
            })
            .ToListAsync();

        return Ok(users);
    }


    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> Get(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null) return NotFound();
        return Ok(user);
    }

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
                LastSeenUtc = x.LastSeenUtc,
                PhoneNumber = x.PhoneNumber
            })
            .FirstOrDefaultAsync();

        if (u == null) return NotFound();
        return Ok(u);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var u = await _context.Users
            .Where(x => x.Id == userId)
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

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        user.DisplayName = req.DisplayName?.Trim();
        user.AvatarUrl = req.AvatarUrl?.Trim();
        user.Bio = req.Bio?.Trim();

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
[HttpPost("avatar")]
[Consumes("multipart/form-data")]
[RequestFormLimits(MultipartBodyLengthLimit = 10_000_000)]
[RequestSizeLimit(10_000_000)]
public async Task<IActionResult> UploadAvatar([FromForm] AvatarUploadRequest model)
{
    var file = model.File;
    if (file == null || file.Length == 0)
        return BadRequest("No file.");

    var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowed.Contains(ext))
        return BadRequest("Invalid file type.");

    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // wwwroot/uploads/avatars/{userId}/yyyyMMddHHmmssfff.ext
    var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    var dir = Path.Combine(webRoot, "uploads", "avatars", userId.ToString());
    Directory.CreateDirectory(dir);

    var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
    var fullPath = Path.Combine(dir, fileName);

    using (var stream = new FileStream(fullPath, FileMode.Create))
        await file.CopyToAsync(stream);

    var relativeUrl = $"/uploads/avatars/{userId}/{fileName}";
    return Ok(new { url = relativeUrl });
}






    [HttpGet("check-username")]
    public async Task<IActionResult> CheckUsername([FromQuery] string u)
    {
        if (string.IsNullOrWhiteSpace(u)) return BadRequest();
        var exists = await _context.Users.AnyAsync(x => x.Username == u);
        return Ok(new { available = !exists });
    }

    [Authorize]
    [HttpPatch("display-name")]
    public async Task<IActionResult> UpdateDisplayName([FromBody] string dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto))
            return BadRequest("Invalid display name");

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user == null) return NotFound();

        user.DisplayName = dto.Trim();
        await _context.SaveChangesAsync();
        return Ok();
    }

}
