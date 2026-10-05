namespace Phichat.Application.DTOs.Keys;

/// <summary>The private key encrypted on the client with a passphrase-derived key (all base64).</summary>
public class KeyBackupDto
{
    public string Ciphertext { get; set; } = default!;
    public string Salt { get; set; } = default!;
    public string Iv { get; set; } = default!;
    public string Kdf { get; set; } = default!;
    public int Iterations { get; set; }
}

/// <summary>A user's public identity key, as other users see it.</summary>
public class PublicIdentityKeyDto
{
    public Guid UserId { get; set; }
    public string KeyId { get; set; } = default!;
    public string PublicKey { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}

/// <summary>The caller's own active identity key with its backup.</summary>
public class MyIdentityKeyDto
{
    public string KeyId { get; set; } = default!;
    public string PublicKey { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public KeyBackupDto Backup { get; set; } = default!;
}

/// <summary>Publishes a new identity key (first setup, or a reset that replaces the current one).</summary>
public class PublishIdentityKeyRequest
{
    public string PublicKey { get; set; } = default!;
    public KeyBackupDto Backup { get; set; } = default!;
}

/// <summary>Re-encrypts the backup of the active key (recovery passphrase change).</summary>
public class UpdateKeyBackupRequest
{
    public string KeyId { get; set; } = default!;
    public KeyBackupDto Backup { get; set; } = default!;
}
