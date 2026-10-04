namespace Phichat.Application.DTOs.Auth;

public class RegisterWithPhoneRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Token from <c>POST /api/auth/verify-phone</c>; the phone number is taken from it.</summary>
    public string RegistrationToken { get; set; } = string.Empty;
}
