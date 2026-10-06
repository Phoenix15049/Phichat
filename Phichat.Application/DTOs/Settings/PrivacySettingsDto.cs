namespace Phichat.Application.DTOs.Settings;

public class PrivacySettingsDto
{
    /// <summary>"everyone", "contacts" or "nobody".</summary>
    public string LastSeen { get; set; } = "everyone";

    public bool ReadReceipts { get; set; } = true;
}
