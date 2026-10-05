using Phichat.Application.DTOs.Keys;

namespace Phichat.Application.Interfaces;

/// <summary>End-to-end encryption identity keys (public keys and passphrase-encrypted backups).</summary>
public interface IIdentityKeyService
{
    /// <summary>The caller's active key with its backup, or null when none was published yet.</summary>
    Task<MyIdentityKeyDto?> GetMyKeyAsync(Guid userId);

    /// <summary>Publishes the first identity key; fails if one is already active.</summary>
    Task<MyIdentityKeyDto> CreateAsync(Guid userId, PublishIdentityKeyRequest request);

    /// <summary>Revokes the active key (if any) and publishes a new one.</summary>
    Task<MyIdentityKeyDto> ReplaceAsync(Guid userId, PublishIdentityKeyRequest request);

    /// <summary>Stores a re-encrypted backup of the active key (passphrase change).</summary>
    Task UpdateBackupAsync(Guid userId, UpdateKeyBackupRequest request);

    /// <summary>A user's active public key.</summary>
    Task<PublicIdentityKeyDto> GetActiveKeyAsync(Guid userId);

    /// <summary>A specific (possibly revoked) public key of a user.</summary>
    Task<PublicIdentityKeyDto> GetKeyAsync(Guid userId, string keyId);

    /// <summary>
    /// Checks that a message body is in the end-to-end format and encrypted for the sender's and
    /// the receiver's active keys, so a stale client re-encrypts instead of sending unreadable data.
    /// </summary>
    Task EnsureMessageKeysAsync(Guid senderId, Guid receiverId, string encryptedText);
}
