using Microsoft.EntityFrameworkCore;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Keys;
using Phichat.Application.Interfaces;
using Phichat.Application.Security;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;
using Phichat.Infrastructure.Security;

namespace Phichat.Infrastructure.Services;

public class IdentityKeyService : IIdentityKeyService
{
    private readonly AppDbContext _db;

    public IdentityKeyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MyIdentityKeyDto?> GetMyKeyAsync(Guid userId)
    {
        var key = await ActiveKeys(userId).AsNoTracking().FirstOrDefaultAsync();
        return key == null ? null : ToMine(key);
    }

    public async Task<MyIdentityKeyDto> CreateAsync(Guid userId, PublishIdentityKeyRequest request)
    {
        var key = NewKey(userId, request);

        if (await ActiveKeys(userId).AnyAsync())
            throw new ConflictException("identity_key_exists", "An identity key is already published for this account.");

        _db.UserIdentityKeys.Add(key);
        await SaveNewKeyAsync();
        return ToMine(key);
    }

    public async Task<MyIdentityKeyDto> ReplaceAsync(Guid userId, PublishIdentityKeyRequest request)
    {
        var key = NewKey(userId, request);
        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync();

        // The old private key is gone for good: drop its backup, keep the public key for history.
        await ActiveKeys(userId).ExecuteUpdateAsync(s => s
            .SetProperty(k => k.RevokedAtUtc, now)
            .SetProperty(k => k.BackupCiphertext, (string?)null)
            .SetProperty(k => k.BackupSalt, (string?)null)
            .SetProperty(k => k.BackupIv, (string?)null)
            .SetProperty(k => k.BackupKdf, (string?)null)
            .SetProperty(k => k.BackupIterations, (int?)null));

        _db.UserIdentityKeys.Add(key);
        await SaveNewKeyAsync();
        await tx.CommitAsync();

        return ToMine(key);
    }

    public async Task UpdateBackupAsync(Guid userId, UpdateKeyBackupRequest request)
    {
        var backup = request.Backup;
        var updated = await ActiveKeys(userId)
            .Where(k => k.KeyId == request.KeyId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(k => k.BackupCiphertext, backup.Ciphertext)
                .SetProperty(k => k.BackupSalt, backup.Salt)
                .SetProperty(k => k.BackupIv, backup.Iv)
                .SetProperty(k => k.BackupKdf, backup.Kdf)
                .SetProperty(k => k.BackupIterations, backup.Iterations)
                .SetProperty(k => k.BackupUpdatedAtUtc, DateTime.UtcNow));

        if (updated == 0)
            throw new ConflictException("identity_key_changed", "The identity key was replaced; reload it before changing the backup.");
    }

    public async Task<PublicIdentityKeyDto> GetActiveKeyAsync(Guid userId)
    {
        var key = await ActiveKeys(userId).AsNoTracking().FirstOrDefaultAsync();
        return key == null
            ? throw new NotFoundException("no_identity_key", "This user has not set up end-to-end encryption yet.")
            : ToPublic(key);
    }

    public async Task<PublicIdentityKeyDto> GetKeyAsync(Guid userId, string keyId)
    {
        var key = await _db.UserIdentityKeys.AsNoTracking()
            .FirstOrDefaultAsync(k => k.UserId == userId && k.KeyId == keyId);

        return key == null
            ? throw new NotFoundException("identity_key_not_found", "Identity key not found.")
            : ToPublic(key);
    }

    public async Task EnsureMessageKeysAsync(Guid senderId, Guid receiverId, string encryptedText)
    {
        if (!EncryptedMessageFormat.TryParse(encryptedText, out var senderKeyId, out var recipientKeyId))
            throw new BadRequestException("invalid_encryption", "Messages must be end-to-end encrypted.");

        var active = await _db.UserIdentityKeys.AsNoTracking()
            .Where(k => (k.UserId == senderId || k.UserId == receiverId) && k.RevokedAtUtc == null)
            .Select(k => new { k.UserId, k.KeyId })
            .ToListAsync();

        var senderKey = active.FirstOrDefault(k => k.UserId == senderId)?.KeyId;
        var receiverKey = active.FirstOrDefault(k => k.UserId == receiverId)?.KeyId;

        if (senderKey == null || senderKey != senderKeyId)
            throw new ConflictException("sender_key_outdated", "Your encryption key changed on another device. Reload and try again.");

        if (receiverKey == null)
            throw new ConflictException("recipient_no_key", "This user has not set up end-to-end encryption yet.");

        // For a message to yourself both ids are the same key.
        if (receiverKey != recipientKeyId)
            throw new ConflictException("recipient_key_changed", "The recipient's encryption key changed. The message must be encrypted again.");
    }

    // ---- helpers ----

    private IQueryable<UserIdentityKey> ActiveKeys(Guid userId) =>
        _db.UserIdentityKeys.Where(k => k.UserId == userId && k.RevokedAtUtc == null);

    private static UserIdentityKey NewKey(Guid userId, PublishIdentityKeyRequest request)
    {
        var spki = IdentityKeyMath.ParseP256PublicKey(request.PublicKey)
            ?? throw new BadRequestException("invalid_public_key", "The public key must be an uncompressed P-256 key in SPKI format.");

        var now = DateTime.UtcNow;
        return new UserIdentityKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            KeyId = IdentityKeyMath.KeyIdOf(spki),
            PublicKey = Convert.ToBase64String(spki),
            CreatedAtUtc = now,
            BackupCiphertext = request.Backup.Ciphertext,
            BackupSalt = request.Backup.Salt,
            BackupIv = request.Backup.Iv,
            BackupKdf = request.Backup.Kdf,
            BackupIterations = request.Backup.Iterations,
            BackupUpdatedAtUtc = now
        };
    }

    /// <summary>
    /// Unique indexes (one active key per user, globally unique key ids) turn races and
    /// re-used keys into a conflict instead of a second active key.
    /// </summary>
    private async Task SaveNewKeyAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("identity_key_conflict", "The identity key could not be published. Reload and try again.");
        }
    }

    private static MyIdentityKeyDto ToMine(UserIdentityKey k) => new()
    {
        KeyId = k.KeyId,
        PublicKey = k.PublicKey,
        CreatedAtUtc = k.CreatedAtUtc,
        Backup = new KeyBackupDto
        {
            Ciphertext = k.BackupCiphertext ?? "",
            Salt = k.BackupSalt ?? "",
            Iv = k.BackupIv ?? "",
            Kdf = k.BackupKdf ?? "",
            Iterations = k.BackupIterations ?? 0
        }
    };

    private static PublicIdentityKeyDto ToPublic(UserIdentityKey k) => new()
    {
        UserId = k.UserId,
        KeyId = k.KeyId,
        PublicKey = k.PublicKey,
        CreatedAtUtc = k.CreatedAtUtc,
        RevokedAtUtc = k.RevokedAtUtc
    };
}
