namespace Phichat.Infrastructure.Push;

/// <summary>Bound from the "Push" configuration section.</summary>
public sealed class PushOptions
{
    public const string SectionName = "Push";

    /// <summary>VAPID contact (mailto: or https: URL) given to push services.</summary>
    public string Subject { get; set; } = "mailto:admin@example.com";

    /// <summary>
    /// VAPID key pair (base64url: 65-byte public point, 32-byte private scalar). When empty, a pair is
    /// generated once and kept in App_Data/vapid-keys.json; replacing it invalidates every subscription.
    /// </summary>
    public string? VapidPublicKey { get; set; }
    public string? VapidPrivateKey { get; set; }
}
