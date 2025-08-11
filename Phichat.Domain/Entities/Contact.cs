namespace Phichat.Domain.Entities;

public class Contact
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid ContactId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Owner { get; set; } = default!;
    public User ContactUser { get; set; } = default!;
}
