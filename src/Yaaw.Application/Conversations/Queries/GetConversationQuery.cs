using MediatR;
using Yaaw.Application.DTOs.Conversations;
using Yaaw.Domain.Interfaces;

namespace Yaaw.Application.Conversations.Queries;

public sealed record GetConversationQuery(Guid Id, string UserId) : IRequest<ConversationDto?>;

internal sealed class GetConversationHandler(IConversationRepository repository)
    : IRequestHandler<GetConversationQuery, ConversationDto?>
{
    public async Task<ConversationDto?> Handle(GetConversationQuery request, CancellationToken ct)
    {
        var conversation = await repository.GetByIdAsync(request.Id, request.UserId, ct);

        if (conversation is null)
        {
            return null;
        }

        return conversation.ToDto();
    }
}
