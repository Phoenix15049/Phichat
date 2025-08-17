namespace Phichat.Domain.Entities;

public class PhoneVerification
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = default!;
    public string CodeHash { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public bool Consumed { get; set; } = false;
    public int Attempts { get; set; } = 0;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
