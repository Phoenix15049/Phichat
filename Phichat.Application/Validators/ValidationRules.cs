using FluentValidation;

namespace Phichat.Application.Validators;

/// <summary>Shared input rules so every endpoint enforces the same limits.</summary>
public static class ValidationRules
{
    public const int UsernameMinLength = 4;
    public const int UsernameMaxLength = 32;
    public const string UsernamePattern = @"^[A-Za-z0-9_]+$";

    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    /// <summary>E.164 phone number, e.g. +989121234567.</summary>
    public const string PhonePattern = @"^\+[1-9]\d{7,14}$";

    public const string SmsCodePattern = @"^\d{6}$";

    /// <summary>Upper bound for an encrypted message body (base64 of IV + ciphertext).</summary>
    public const int EncryptedTextMaxLength = 64 * 1024;

    /// <summary>
    /// A group message also carries one wrapped message key per member (about 105 characters each),
    /// so its body may be larger. The exact limit per chat type is enforced by the message service.
    /// </summary>
    public const int GroupEncryptedTextMaxLength = EncryptedTextMaxLength + 32 * 1024;

    public const int GroupTitleMaxLength = 64;
    public const int GroupDescriptionMaxLength = 255;

    /// <summary>Members per group, including the owner (bounded by the per-member message keys).</summary>
    public const int GroupMaxMembers = 200;

    public const int DisplayNameMaxLength = 64;
    public const int BioMaxLength = 300;
    public const int EmojiMaxLength = 16;

    public static IRuleBuilderOptions<T, string> ValidUsername<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Username is required.")
            .Length(UsernameMinLength, UsernameMaxLength)
            .WithMessage($"Username must be {UsernameMinLength}-{UsernameMaxLength} characters.")
            .Matches(UsernamePattern)
            .WithMessage("Username may contain only English letters, digits and underscore.");

    public static IRuleBuilderOptions<T, string> ValidNewPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Password is required.")
            .Length(PasswordMinLength, PasswordMaxLength)
            .WithMessage($"Password must be {PasswordMinLength}-{PasswordMaxLength} characters.");

    public static IRuleBuilderOptions<T, string> ValidPhone<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Phone number is required.")
            .Matches(PhonePattern).WithMessage("Phone number must be in international format, e.g. +989121234567.");

    public static IRuleBuilderOptions<T, string> ValidSmsCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Code is required.")
            .Matches(SmsCodePattern).WithMessage("Code must be 6 digits.");
}
