using FluentValidation;
using Phichat.Application.DTOs.Auth;

public class LoginWithSmsRequestValidator : AbstractValidator<LoginWithSmsRequest>
{
    public LoginWithSmsRequestValidator()
    {
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(@"^\+?[1-9]\d{7,14}$");
        RuleFor(x => x.Code).NotEmpty().Length(4, 8);
    }
}
