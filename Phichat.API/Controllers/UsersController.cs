using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserQueryService _userQueryService;
    private readonly AppDbContext _context;
    public UsersController(AppDbContext context, IUserQueryService userQueryService)
    {
        _context = context;
        _userQueryService = userQueryService;
    }

    [HttpGet("{username}/public-key")]
    public async Task<IActionResult> GetPublicKey(string username)
    {
        var result = await _userQueryService.GetByUsernameAsync(username);
        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("id/{id:guid}/public-key")]
    public async Task<IActionResult> GetPublicKeyById(Guid id)
    {
        var result = await _userQueryService.GetByIdAsync(id);
        if (result == null)
            return NotFound();

        return Ok(result);
    }


    [Authorize]
    [HttpGet("list")]
    public async Task<IActionResult> GetUsers()
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var users = await _context.Users
            .Where(u => u.Id != currentUserId)
            .Select(u => new
            {
                u.Id,
                u.Username
            })
            .ToListAsync();

        return Ok(users);
    }

}
