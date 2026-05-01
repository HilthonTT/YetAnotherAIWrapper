using MediatR;
using Yaaw.Domain.Interfaces;

namespace Yaaw.Application.Conversations.Commands;

public sealed record DeleteConversationCommand(Guid Id, string UserId) : IRequest<bool>;

internal sealed class DeleteConversationHandler(IConversationRepository repository)
    : IRequestHandler<DeleteConversationCommand, bool>
{
    public async Task<bool> Handle(DeleteConversationCommand request, CancellationToken ct)
    {
        return await repository.DeleteAsync(request.Id, request.UserId, ct);
    }
}
