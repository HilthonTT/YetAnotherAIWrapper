using Yaaw.Application.DTOs.Messages;

namespace Yaaw.Application.Interfaces;

public interface IChatStreamingCoordinator
{
    Task AddStreamingMessage(Guid conversationId, string text);
    IAsyncEnumerable<ClientMessageFragmentDto> StreamFragments(Guid conversationId, Guid? lastMessageId, Guid? lastDeliveredFragment, CancellationToken cancellationToken = default);
}
