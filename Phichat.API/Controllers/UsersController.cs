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
    private readonly IUserQueryService _userQueryService;
    private readonly AppDbContext _context;
    public UsersController(AppDbContext context, IUserQueryService userQueryService, IUserService userService)
    {
        _context = context;
        _userQueryService = userQueryService;
        _userService = userService;
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
                u.Username,
                u.PublicKey
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



}
