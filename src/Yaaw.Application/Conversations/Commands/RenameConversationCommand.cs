using MediatR;
using Yaaw.Application.DTOs.Conversations;
using Yaaw.Domain.Entities;
using Yaaw.Domain.Interfaces;

namespace Yaaw.Application.Conversations.Commands;

public sealed record RenameConversationCommand(Guid Id, string UserId, string Name) : IRequest<ConversationDto?>;

internal sealed class RenameConversationHandler(IConversationRepository repository)
    : IRequestHandler<RenameConversationCommand, ConversationDto?>
{
    public async Task<ConversationDto?> Handle(RenameConversationCommand request, CancellationToken ct)
    {
        Conversation? conversation = await repository.GetByIdAsync(request.Id, request.UserId, ct);

        if (conversation is null)
        {
            return null;
        }

        conversation.Name = request.Name;
        await repository.SaveChangesAsync(ct);

        return conversation.ToDto();
    }
}
