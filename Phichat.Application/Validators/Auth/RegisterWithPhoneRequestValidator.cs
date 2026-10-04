using FluentValidation;
using Phichat.Application.DTOs.Auth;
using Phichat.Application.Validators;

public class RegisterWithPhoneRequestValidator : AbstractValidator<RegisterWithPhoneRequest>
{
    public RegisterWithPhoneRequestValidator()
    {
        RuleFor(x => x.Username).ValidUsername();
        RuleFor(x => x.Password).ValidNewPassword();
        RuleFor(x => x.RegistrationToken).NotEmpty().WithMessage("Phone verification is required.");
    }
}
