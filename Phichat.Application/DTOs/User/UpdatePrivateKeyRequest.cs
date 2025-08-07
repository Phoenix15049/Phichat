namespace Phichat.Application.DTOs.User;

public class UpdatePrivateKeyRequest
{
    public string EncryptedPrivateKey { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}