namespace Phichat.Application.DTOs.User;

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = default!;
    public string PublicKey { get; set; } = default!;
    public string EncryptedPrivateKey { get; set; } = string.Empty;

}
