namespace Phichat.Application.DTOs.Message;

public class MessageReadResult
{
    public bool Success { get; set; }
    public Guid? SenderId { get; set; }
}