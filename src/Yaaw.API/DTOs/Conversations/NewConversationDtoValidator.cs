using FluentValidation;

namespace Yaaw.API.DTOs.Conversations;

public sealed class NewConversationDtoValidator : AbstractValidator<NewConversationDto>
{
    public NewConversationDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(3).MaximumLength(256);
    }
}
