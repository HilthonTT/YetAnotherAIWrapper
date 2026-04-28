using Microsoft.AspNetCore.SignalR;
using Yaaw.API.DTOs.Messages;
using Yaaw.API.Services;
using System.Runtime.CompilerServices;

namespace Yaaw.API.Hubs;

internal sealed class ChatHub : Hub
{
    public async IAsyncEnumerable<ClientMessageFragmentDto> Stream(
        Guid id, 
        StreamContext context, 
        ChatStreamingCoordinator coordinator,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in coordinator.StreamFragments(id, context.LastMessageId, context.LastMessageId, cancellationToken))
        {
            yield return message;
        }
    }
}
