using Phichat.Application.DTOs.Auth;

namespace Phichat.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task<AuthResult> LoginAsync(LoginRequest request);

    Task RequestSmsCodeAsync(string phoneNumber);

    /// <summary>Consumes an SMS code; signs in an existing account or issues a registration token.</summary>
    Task<VerifyPhoneResult> VerifyPhoneAsync(VerifyPhoneRequest request);

    /// <summary>Consumes an SMS code and signs in; fails if no account uses the phone.</summary>
    Task<AuthResult> LoginWithSmsAsync(LoginWithSmsRequest request);

    Task<AuthResult> RegisterWithPhoneAsync(RegisterWithPhoneRequest request);

    /// <summary>Rotates a refresh token. Reusing a rotated token revokes the whole token family.</summary>
    Task<AuthResult> RefreshAsync(string refreshToken);

    Task LogoutAsync(string? refreshToken);
}
