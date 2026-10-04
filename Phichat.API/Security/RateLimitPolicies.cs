namespace Phichat.API.Security;

public static class RateLimitPolicies
{
    /// <summary>Credential checks (login, register, code verification), per client IP.</summary>
    public const string Auth = "auth";

    /// <summary>Requests that send an SMS, per client IP (per-phone limits live in SmsCodeService).</summary>
    public const string Sms = "sms";

    /// <summary>Token refresh, per client IP.</summary>
    public const string Refresh = "refresh";

    /// <summary>Anonymous lookups such as username availability, per client IP.</summary>
    public const string Lookup = "lookup";

    /// <summary>File and avatar uploads, per authenticated user.</summary>
    public const string Upload = "upload";
}
