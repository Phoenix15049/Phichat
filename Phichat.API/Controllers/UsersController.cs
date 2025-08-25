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
                LastSeenUtc = u.LastSeenUtc
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
                LastSeenUtc = x.LastSeenUtc
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
