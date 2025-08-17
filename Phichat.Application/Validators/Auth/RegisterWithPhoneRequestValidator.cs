using FluentValidation;
using Phichat.Application.DTOs.Auth;

public class RegisterWithPhoneRequestValidator : AbstractValidator<RegisterWithPhoneRequest>
{
    public RegisterWithPhoneRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Invalid phone format.");
    }
}
