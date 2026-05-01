using MediatR;
using Yaaw.Application.Interfaces;
using Yaaw.Domain.Interfaces;

namespace Yaaw.Application.Conversations.Commands;

public sealed record CancelStreamCommand(Guid ConversationId, string UserId) : IRequest<bool>;

internal sealed class CancelStreamHandler(
    IConversationRepository repository,
    ICancellationManager cancellationManager)
    : IRequestHandler<CancelStreamCommand, bool>
{
    public async Task<bool> Handle(CancelStreamCommand request, CancellationToken ct)
    {
        bool owns = await repository.ExistsAsync(request.ConversationId, request.UserId, ct);

        if (!owns)
        {
            return false;
        }

        await cancellationManager.CancelAsync(request.ConversationId);
        return true;
    }
}
