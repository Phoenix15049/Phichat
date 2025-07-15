namespace Phichat.Application.DTOs.Message;

public class SendMessageRequest
{
    public Guid ReceiverId { get; set; }
    public string EncryptedText { get; set; } // متن رمز‌شده AES در Base64
}
