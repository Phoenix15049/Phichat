namespace Phichat.Application.DTOs.Auth;

public class VerifyPhoneRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Result of verifying a phone code. Either the phone belongs to an account (the caller is signed in)
/// or it does not, and <see cref="RegistrationToken"/> proves the phone was verified for sign-up.
/// </summary>
public class VerifyPhoneResponse
{
    public bool IsNewUser { get; set; }
    public string? RegistrationToken { get; set; }
    public AuthResponse? Auth { get; set; }
}

/// <summary>Internal counterpart of <see cref="VerifyPhoneResponse"/> carrying the refresh token.</summary>
public class VerifyPhoneResult
{
    public bool IsNewUser { get; set; }
    public string? RegistrationToken { get; set; }
    public AuthResult? Auth { get; set; }
}
