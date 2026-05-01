using MediatR;
using Yaaw.Application.DTOs.Conversations;
using Yaaw.Domain.Entities;
using Yaaw.Domain.Interfaces;

namespace Yaaw.Application.Conversations.Commands;

public sealed record CreateConversationCommand(string Name, string UserId) : IRequest<ConversationDto>;

internal sealed class CreateConversationHandler(IConversationRepository repository)
    : IRequestHandler<CreateConversationCommand, ConversationDto>
{
    public async Task<ConversationDto> Handle(CreateConversationCommand request, CancellationToken ct)
    {
        var conversation = new Conversation
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            UserId = request.UserId,
            Messages = [],
        };

        await repository.AddAsync(conversation, ct);
        await repository.SaveChangesAsync(ct);

        return conversation.ToDto();
    }
}
