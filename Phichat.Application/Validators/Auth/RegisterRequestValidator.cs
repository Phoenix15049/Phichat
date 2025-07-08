using FluentValidation;
using Phichat.Application.DTOs.Auth;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MinimumLength(3).MaximumLength(50);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6);

        RuleFor(x => x.PublicKey)
            .NotEmpty().WithMessage("Public key is required.");
    }
}
