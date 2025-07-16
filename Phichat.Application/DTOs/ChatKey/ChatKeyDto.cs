namespace Phichat.Application.DTOs.ChatKey;

public class ChatKeyDto
{
    public Guid ReceiverId { get; set; }
    public string EncryptedKey { get; set; } = string.Empty;
}

