using Microsoft.AspNetCore.Http;

public class SendMessageWithFileRequest
{
    public Guid ReceiverId { get; set; }
    public string EncryptedText { get; set; } = string.Empty;
    public IFormFile File { get; set; } = default!;
    public Guid? ReplyToMessageId { get; set; }

}
