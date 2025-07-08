namespace Phichat.Application.DTOs.Auth;

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
}
