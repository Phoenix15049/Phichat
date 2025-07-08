using Microsoft.AspNetCore.Http;

public class SendMessageWithFileRequest
{
    public Guid ReceiverId { get; set; }
    public string? PlainText { get; set; }
    public IFormFile File { get; set; } = default!;
}
