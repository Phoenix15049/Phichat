namespace Phichat.Application.DTOs.Auth;

public class LoginWithSmsRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
