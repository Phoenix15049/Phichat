using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phichat.Application.DTOs.Auth;
using Phichat.Application.Interfaces;
using System.Security.Claims;

namespace Phichat.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await _userService.RegisterAsync(request);
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _userService.LoginAsync(request);
        return Ok(result);
    }



    [HttpPost("register-phone")]
    public async Task<IActionResult> RegisterWithPhone([FromBody] RegisterWithPhoneRequest request)
    {
        var result = await _userService.RegisterWithPhoneAsync(request);
        return Ok(result);
    }

    [HttpPost("request-sms-code")]
    public async Task<IActionResult> RequestSmsCode([FromBody] RequestSmsCodeRequest request)
    {
        await _userService.RequestSmsCodeAsync(request.PhoneNumber);
        return Ok();
    }

    [HttpPost("login-sms")]
    public async Task<IActionResult> LoginWithSms([FromBody] LoginWithSmsRequest request)
    {
        var result = await _userService.LoginWithSmsAsync(request);
        return Ok(result);
    }




}
