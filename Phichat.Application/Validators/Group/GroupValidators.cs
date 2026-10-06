using FluentValidation;
using Phichat.Application.DTOs.Group;

namespace Phichat.Application.Validators.Group;

public class CreateGroupRequestValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupRequestValidator()
    {
        RuleFor(x => x.Title).Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Title is required.")
            .MaximumLength(ValidationRules.GroupTitleMaxLength);
        RuleFor(x => x.MemberIds).NotNull()
            .Must(ids => ids.Count < ValidationRules.GroupMaxMembers)
            .WithMessage($"A group can have at most {ValidationRules.GroupMaxMembers} members.");
    }
}

public class UpdateGroupRequestValidator : AbstractValidator<UpdateGroupRequest>
{
    public UpdateGroupRequestValidator()
    {
        RuleFor(x => x.Title).Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Title is required.")
            .MaximumLength(ValidationRules.GroupTitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(ValidationRules.GroupDescriptionMaxLength);
    }
}

public class AddGroupMembersRequestValidator : AbstractValidator<AddGroupMembersRequest>
{
    public AddGroupMembersRequestValidator()
    {
        RuleFor(x => x.UserIds).NotEmpty().Must(ids => ids.Count < ValidationRules.GroupMaxMembers);
    }
}

public class SetGroupRoleRequestValidator : AbstractValidator<SetGroupRoleRequest>
{
    public SetGroupRoleRequestValidator()
    {
        RuleFor(x => x.Role).Must(r => r is "admin" or "member").WithMessage("Role must be 'admin' or 'member'.");
    }
}
