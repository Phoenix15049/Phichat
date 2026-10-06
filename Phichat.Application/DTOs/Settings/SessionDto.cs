namespace Phichat.Application.DTOs.Settings;

/// <summary>A signed-in device (one refresh-token family).</summary>
public class SessionDto
{
    public Guid Id { get; set; }
    public string? DeviceName { get; set; }
    public string? IpAddress { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastActiveAtUtc { get; set; }
    public bool IsCurrent { get; set; }
}
