using FluentValidation;

namespace Yaaw.API.DTOs.Conversations;

public sealed class RenameConversationDtoValidator : AbstractValidator<RenameConversationDto>
{
    public RenameConversationDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}