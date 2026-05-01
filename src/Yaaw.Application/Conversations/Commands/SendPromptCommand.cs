using MediatR;
using Yaaw.Application.Interfaces;
using Yaaw.Domain.Interfaces;

namespace Yaaw.Application.Conversations.Commands;

public sealed record SendPromptCommand(Guid ConversationId, string UserId, string Text) : IRequest<bool>;

internal sealed class SendPromptHandler(
    IConversationRepository repository,
    IChatStreamingCoordinator streamingCoordinator)
    : IRequestHandler<SendPromptCommand, bool>
{
    public async Task<bool> Handle(SendPromptCommand request, CancellationToken ct)
    {
        bool owns = await repository.ExistsAsync(request.ConversationId, request.UserId, ct);

        if (!owns)
        {
            return false;
        }

        await streamingCoordinator.AddStreamingMessage(request.ConversationId, request.Text);

        return true;
    }
}
