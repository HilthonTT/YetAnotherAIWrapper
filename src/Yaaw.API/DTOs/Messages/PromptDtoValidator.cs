using FluentValidation;

namespace Yaaw.API.DTOs.Messages;

public sealed class PromptDtoValidator : AbstractValidator<PromptDto>
{
    public PromptDtoValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MinimumLength(1).MaximumLength(4096);
    }
}
