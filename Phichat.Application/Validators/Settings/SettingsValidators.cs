using FluentValidation;
using Phichat.Application.DTOs.Settings;

namespace Phichat.Application.Validators.Settings;

public class PrivacySettingsValidator : AbstractValidator<PrivacySettingsDto>
{
    public PrivacySettingsValidator()
    {
        RuleFor(x => x.LastSeen)
            .Must(v => v is "everyone" or "contacts" or "nobody")
            .WithMessage("LastSeen must be 'everyone', 'contacts' or 'nobody'.");
    }
}

public class PushSubscriptionRequestValidator : AbstractValidator<PushSubscriptionRequest>
{
    public PushSubscriptionRequestValidator()
    {
        RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(2048);
        // 65-byte uncompressed P-256 point and 16-byte secret, base64url.
        RuleFor(x => x.P256dh).NotEmpty().Matches("^[A-Za-z0-9_-]{86,88}={0,2}$");
        RuleFor(x => x.Auth).NotEmpty().Matches("^[A-Za-z0-9_-]{22,24}={0,2}$");
        RuleFor(x => x.Lang).Must(v => v is "fa" or "en").WithMessage("Lang must be 'fa' or 'en'.");
    }
}
