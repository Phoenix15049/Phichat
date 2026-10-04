using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Phichat.API.Security;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Auth;
using Phichat.Application.Interfaces;

namespace Phichat.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly RefreshTokenCookieOptions _cookieOptions;

    public AuthController(IAuthService authService, IOptions<RefreshTokenCookieOptions> cookieOptions)
    {
        _authService = authService;
        _cookieOptions = cookieOptions.Value;
    }

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        return SignedIn(await _authService.RegisterAsync(request));
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        return SignedIn(await _authService.LoginAsync(request));
    }

    [HttpPost("request-sms-code")]
    [EnableRateLimiting(RateLimitPolicies.Sms)]
    public async Task<IActionResult> RequestSmsCode([FromBody] RequestSmsCodeRequest request)
    {
        await _authService.RequestSmsCodeAsync(request.PhoneNumber);
        return NoContent();
    }

    /// <summary>
    /// Verifies an SMS code. Signs in when the phone has an account, otherwise returns a
    /// registration token for <c>register-phone</c>.
    /// </summary>
    [HttpPost("verify-phone")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<VerifyPhoneResponse>> VerifyPhone([FromBody] VerifyPhoneRequest request)
    {
        var result = await _authService.VerifyPhoneAsync(request);

        if (result.Auth != null)
            SetRefreshCookie(result.Auth);

        return Ok(new VerifyPhoneResponse
        {
            IsNewUser = result.IsNewUser,
            RegistrationToken = result.RegistrationToken,
            Auth = result.Auth?.Response
        });
    }

    [HttpPost("login-sms")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<AuthResponse>> LoginWithSms([FromBody] LoginWithSmsRequest request)
    {
        return SignedIn(await _authService.LoginWithSmsAsync(request));
    }

    [HttpPost("register-phone")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<AuthResponse>> RegisterWithPhone([FromBody] RegisterWithPhoneRequest request)
    {
        return SignedIn(await _authService.RegisterWithPhoneAsync(request));
    }

    /// <summary>Exchanges the refresh-token cookie for a new access token (and rotates the cookie).</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    public async Task<ActionResult<AuthResponse>> Refresh()
    {
        var refreshToken = Request.Cookies[_cookieOptions.Name];

        try
        {
            return SignedIn(await _authService.RefreshAsync(refreshToken ?? string.Empty));
        }
        catch (UnauthorizedException)
        {
            // Only a rejected token ends the session; transient failures keep the cookie for a retry.
            ClearRefreshCookie();
            throw;
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync(Request.Cookies[_cookieOptions.Name]);
        ClearRefreshCookie();
        return NoContent();
    }

    private ActionResult<AuthResponse> SignedIn(AuthResult result)
    {
        SetRefreshCookie(result);
        return Ok(result.Response);
    }

    private void SetRefreshCookie(AuthResult result)
    {
        Response.Cookies.Append(_cookieOptions.Name, result.RefreshToken, BuildCookieOptions(result.RefreshTokenExpiresAtUtc));
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(_cookieOptions.Name, BuildCookieOptions(expires: null));
    }

    private CookieOptions BuildCookieOptions(DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = _cookieOptions.SameSite,
        Path = _cookieOptions.Path,
        Expires = expires.HasValue ? new DateTimeOffset(expires.Value, TimeSpan.Zero) : null,
        IsEssential = true
    };
}
