namespace Phichat.Domain.Entities;

/// <summary>
/// A browser's Web Push subscription, tied to the sign-in session that registered it
/// (ending the session removes it). Only notification metadata is pushed, never message content.
/// </summary>
public class PushSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>The refresh-token family (session) of the device.</summary>
    public Guid SessionId { get; set; }

    public string Endpoint { get; set; } = default!;

    /// <summary>Hex SHA-256 of <see cref="Endpoint"/>, for a unique index.</summary>
    public string EndpointHash { get; set; } = default!;

    /// <summary>The browser's P-256 public key (base64url, 65 bytes uncompressed).</summary>
    public string P256dh { get; set; } = default!;

    /// <summary>The browser's 16-byte auth secret (base64url).</summary>
    public string Auth { get; set; } = default!;

    /// <summary>UI language for the notification text ("fa" or "en").</summary>
    public string Lang { get; set; } = "fa";

    /// <summary>Whether the notification may name the sender.</summary>
    public bool ShowSender { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = default!;
}
