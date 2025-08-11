namespace Phichat.Application.DTOs.Contact;

public class ContactDto
{
    public Guid ContactId { get; set; }
    public string Username { get; set; } = default!;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
}
