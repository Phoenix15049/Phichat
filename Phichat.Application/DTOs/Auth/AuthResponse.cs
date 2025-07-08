namespace Phichat.Application.DTOs.Auth;

public class SendMessageRequest
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}
