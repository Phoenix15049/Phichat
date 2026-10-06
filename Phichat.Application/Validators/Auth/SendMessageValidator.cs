using FluentValidation;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Validators;

public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x).Must(x => (x.ReceiverId != Guid.Empty) != x.GroupId.HasValue)
            .WithMessage("Set either ReceiverId or GroupId.");
        RuleFor(x => x.EncryptedText).NotEmpty().MaximumLength(ValidationRules.GroupEncryptedTextMaxLength);
    }
}

public class SendMessageWithFileRequestValidator : AbstractValidator<SendMessageWithFileRequest>
{
    public SendMessageWithFileRequestValidator()
    {
        RuleFor(x => x).Must(x => (x.ReceiverId != Guid.Empty) != x.GroupId.HasValue)
            .WithMessage("Set either ReceiverId or GroupId.");
        RuleFor(x => x.EncryptedText).NotEmpty().MaximumLength(ValidationRules.GroupEncryptedTextMaxLength);
        RuleFor(x => x.File).NotNull().WithMessage("File is required.");
    }
}

public class EditMessageRequestValidator : AbstractValidator<EditMessageRequest>
{
    public EditMessageRequestValidator()
    {
        RuleFor(x => x.EncryptedText).NotEmpty().MaximumLength(ValidationRules.GroupEncryptedTextMaxLength);
    }
}

public class ReactionRequestValidator : AbstractValidator<ReactionRequest>
{
    public ReactionRequestValidator()
    {
        RuleFor(x => x.Emoji).NotEmpty().MaximumLength(ValidationRules.EmojiMaxLength);
    }
}
