using Microsoft.AspNetCore.Mvc;
using Phichat.Application.Interfaces;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserQueryService _userQueryService;

    public UsersController(IUserQueryService userQueryService)
    {
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
}
