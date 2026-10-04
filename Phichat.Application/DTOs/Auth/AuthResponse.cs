namespace Phichat.Application.DTOs.Auth;

/// <summary>Returned by every successful sign-in. The refresh token travels in an HttpOnly cookie, not here.</summary>
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
}

/// <summary>Internal result of a sign-in: the public response plus the refresh token for the cookie.</summary>
public class AuthResult
{
    public AuthResponse Response { get; set; } = new();
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAtUtc { get; set; }
}
