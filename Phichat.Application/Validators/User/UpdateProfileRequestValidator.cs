using FluentValidation;
using Phichat.Application.DTOs.User;
using Phichat.Application.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName).MaximumLength(ValidationRules.DisplayNameMaxLength);
        RuleFor(x => x.Bio).MaximumLength(ValidationRules.BioMaxLength);
        RuleFor(x => x.AvatarUrl).MaximumLength(512);
    }
}
