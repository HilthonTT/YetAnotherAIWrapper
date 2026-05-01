using FluentValidation;

namespace Yaaw.Application.DTOs.Conversations;

public sealed class RenameConversationDtoValidator : AbstractValidator<RenameConversationDto>
{
    public RenameConversationDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
