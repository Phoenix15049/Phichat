using FluentValidation;
using Phichat.Application.DTOs.Auth;
using Phichat.Application.Validators;

public class LoginWithSmsRequestValidator : AbstractValidator<LoginWithSmsRequest>
{
    public LoginWithSmsRequestValidator()
    {
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.Code).ValidSmsCode();
    }
}

public class VerifyPhoneRequestValidator : AbstractValidator<VerifyPhoneRequest>
{
    public VerifyPhoneRequestValidator()
    {
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.Code).ValidSmsCode();
    }
}
