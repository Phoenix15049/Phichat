public class SendMessageViaHubRequest
{
    public Guid ReceiverId { get; set; }
    public string EncryptedText { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? FileBase64 { get; set; }
    public Guid? ReplyToMessageId { get; set; }

}
