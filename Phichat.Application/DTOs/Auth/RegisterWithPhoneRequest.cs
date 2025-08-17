namespace Phichat.Application.DTOs.Auth;

public class RegisterWithPhoneRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}
