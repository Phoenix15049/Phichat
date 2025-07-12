public class SendMessageViaHubRequest
{
    public Guid ReceiverId { get; set; }
    public string PlainText { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? FileBase64 { get; set; }
}
