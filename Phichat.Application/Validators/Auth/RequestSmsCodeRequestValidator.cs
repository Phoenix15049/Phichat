using FluentValidation;
using Phichat.Application.DTOs.Auth;
using Phichat.Application.Validators;

public class RequestSmsCodeRequestValidator : AbstractValidator<RequestSmsCodeRequest>
{
    public RequestSmsCodeRequestValidator()
    {
        RuleFor(x => x.PhoneNumber).ValidPhone();
    }
}
