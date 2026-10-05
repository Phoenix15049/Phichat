namespace Phichat.Domain.Entities;

/// <summary>
/// A user's end-to-end encryption identity: an ECDH P-256 public key. The private key never
/// reaches the server in the clear; the active key carries a backup of it, encrypted on the
/// client with a key derived from the user's recovery passphrase.
/// Replaced keys are kept (revoked) so peers can still decrypt messages exchanged with them.
/// </summary>
public class UserIdentityKey
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>base64url of the first 16 bytes of SHA-256(<see cref="PublicKey"/>).</summary>
    public string KeyId { get; set; } = default!;

    /// <summary>The public key as base64 SubjectPublicKeyInfo (SPKI).</summary>
    public string PublicKey { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    // Passphrase-encrypted private key (PKCS#8). Cleared when the key is revoked.
    public string? BackupCiphertext { get; set; }
    public string? BackupSalt { get; set; }
    public string? BackupIv { get; set; }
    public string? BackupKdf { get; set; }
    public int? BackupIterations { get; set; }
    public DateTime? BackupUpdatedAtUtc { get; set; }

    public User User { get; set; } = default!;
}
