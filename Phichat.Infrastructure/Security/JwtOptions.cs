namespace Phichat.Infrastructure.Security;

/// <summary>Bound from the "Jwt" configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
    public int RegistrationTokenMinutes { get; set; } = 15;

    /// <summary>Audience of phone-registration tokens; differs from <see cref="Audience"/> so they cannot be used as access tokens.</summary>
    public string RegistrationAudience => Audience + ":phone-registration";
}
