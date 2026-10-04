namespace Phichat.Infrastructure.Security;

/// <summary>Bound from the "SmsCode" configuration section.</summary>
public sealed class SmsCodeOptions
{
    public const string SectionName = "SmsCode";

    public int CodeLifetimeMinutes { get; set; } = 5;

    /// <summary>Wrong guesses allowed per code before it is burned.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Minimum delay between two codes for the same phone.</summary>
    public int ResendCooldownSeconds { get; set; } = 60;

    public int MaxCodesPerHour { get; set; } = 5;
}
