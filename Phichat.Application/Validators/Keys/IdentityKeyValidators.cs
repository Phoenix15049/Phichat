using FluentValidation;
using Phichat.Application.DTOs.Keys;
using Phichat.Application.Security;

namespace Phichat.Application.Validators.Keys;

public static class KeyRules
{
    public const string BackupKdf = "PBKDF2-SHA256";
    public const int MinBackupIterations = 100_000;
    public const int MaxBackupIterations = 10_000_000;

    // An encoded P-256 SPKI is 91 bytes (124 base64 chars); a wrapped PKCS#8 key is ~150 bytes.
    public const int PublicKeyMaxLength = 256;
    public const int BackupCiphertextMaxLength = 1024;

    public static bool IsBase64(string? value, int minBytes, int maxBytes)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var buffer = new byte[(value.Length * 3 + 3) / 4];
        return Convert.TryFromBase64String(value, buffer, out var written) && written >= minBytes && written <= maxBytes;
    }
}

public class KeyBackupValidator : AbstractValidator<KeyBackupDto>
{
    public KeyBackupValidator()
    {
        RuleFor(x => x.Kdf).Equal(KeyRules.BackupKdf).WithMessage($"Kdf must be {KeyRules.BackupKdf}.");
        RuleFor(x => x.Iterations).InclusiveBetween(KeyRules.MinBackupIterations, KeyRules.MaxBackupIterations);
        RuleFor(x => x.Salt).Must(v => KeyRules.IsBase64(v, 16, 64)).WithMessage("Salt must be 16-64 bytes of base64.");
        RuleFor(x => x.Iv).Must(v => KeyRules.IsBase64(v, 12, 12)).WithMessage("IV must be 12 bytes of base64.");
        RuleFor(x => x.Ciphertext)
            .MaximumLength(KeyRules.BackupCiphertextMaxLength)
            .Must(v => KeyRules.IsBase64(v, 32, KeyRules.BackupCiphertextMaxLength))
            .WithMessage("Ciphertext must be base64.");
    }
}

public class PublishIdentityKeyRequestValidator : AbstractValidator<PublishIdentityKeyRequest>
{
    public PublishIdentityKeyRequestValidator()
    {
        RuleFor(x => x.PublicKey).NotEmpty().MaximumLength(KeyRules.PublicKeyMaxLength);
        RuleFor(x => x.Backup).NotNull().SetValidator(new KeyBackupValidator());
    }
}

public class UpdateKeyBackupRequestValidator : AbstractValidator<UpdateKeyBackupRequest>
{
    public UpdateKeyBackupRequestValidator()
    {
        RuleFor(x => x.KeyId).Must(EncryptedMessageFormat.IsValidKeyId).WithMessage("Invalid key id.");
        RuleFor(x => x.Backup).NotNull().SetValidator(new KeyBackupValidator());
    }
}
