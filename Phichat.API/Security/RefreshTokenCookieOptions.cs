namespace Phichat.API.Security;

/// <summary>Bound from the "Auth:RefreshCookie" configuration section.</summary>
public sealed class RefreshTokenCookieOptions
{
    public const string SectionName = "Auth:RefreshCookie";

    public string Name { get; set; } = "phichat_rt";

    /// <summary>The cookie is only sent to the auth endpoints, never to the rest of the API.</summary>
    public string Path { get; set; } = "/api/auth";

    /// <summary>
    /// "Strict" when the client is served from the same site as the API (dev proxy / reverse proxy).
    /// Use "None" only if the client is hosted on a different site; that also requires HTTPS.
    /// </summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Strict;
}
