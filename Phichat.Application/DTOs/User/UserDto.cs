namespace Phichat.Application.DTOs.User;

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = default!;
    public DateTime? LastSeenUtc { get; set; }
}
