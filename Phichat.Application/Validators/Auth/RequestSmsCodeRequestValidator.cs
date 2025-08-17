using FluentValidation;
using Phichat.Application.DTOs.Auth;

public class RequestSmsCodeRequestValidator : AbstractValidator<RequestSmsCodeRequest>
{
    public RequestSmsCodeRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(@"^\+?[1-9]\d{7,14}$");
    }
}
