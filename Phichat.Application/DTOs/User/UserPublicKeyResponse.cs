namespace Phichat.Application.DTOs.User;

public class UserPublicKeyResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
}
