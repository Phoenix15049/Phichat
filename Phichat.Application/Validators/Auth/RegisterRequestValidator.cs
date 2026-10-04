using FluentValidation;
using Phichat.Application.DTOs.Auth;
using Phichat.Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username).ValidUsername();
        RuleFor(x => x.Password).ValidNewPassword();
    }
}
