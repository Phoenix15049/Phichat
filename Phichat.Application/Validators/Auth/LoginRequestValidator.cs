using FluentValidation;
using Phichat.Application.DTOs.Auth;
using Phichat.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // Existing accounts may predate the current username rules, so only bound the length here.
        RuleFor(x => x.Username).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(ValidationRules.PasswordMaxLength);
    }
}
