using FluentValidation;
using Phichat.Application.DTOs.Message;

public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.ReceiverId).NotEmpty();
        RuleFor(x => x.PlainText).NotEmpty();
    }
}
