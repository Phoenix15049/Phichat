namespace Phichat.Application.DTOs.Message;

public class MessageReadResult
{
    public bool Success { get; set; }
    public Guid? SenderId { get; set; }
    public Guid MessageId { get; set; }          
    public DateTime? ReadAtUtc { get; set; }     
}