using FluentValidation;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Validators;

public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.ReceiverId).NotEmpty();
        RuleFor(x => x.EncryptedText).NotEmpty().MaximumLength(ValidationRules.EncryptedTextMaxLength);
    }
}

public class SendMessageWithFileRequestValidator : AbstractValidator<SendMessageWithFileRequest>
{
    public SendMessageWithFileRequestValidator()
    {
        RuleFor(x => x.ReceiverId).NotEmpty();
        RuleFor(x => x.EncryptedText).MaximumLength(ValidationRules.EncryptedTextMaxLength);
        RuleFor(x => x.File).NotNull().WithMessage("File is required.");
    }
}

public class EditMessageRequestValidator : AbstractValidator<EditMessageRequest>
{
    public EditMessageRequestValidator()
    {
        RuleFor(x => x.EncryptedText).NotEmpty().MaximumLength(ValidationRules.EncryptedTextMaxLength);
    }
}

public class ReactionRequestValidator : AbstractValidator<ReactionRequest>
{
    public ReactionRequestValidator()
    {
        RuleFor(x => x.Emoji).NotEmpty().MaximumLength(ValidationRules.EmojiMaxLength);
    }
}
