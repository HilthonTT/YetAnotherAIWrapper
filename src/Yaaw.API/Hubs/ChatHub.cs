using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Yaaw.Application.DTOs.Messages;
using Yaaw.Application.Interfaces;
using System.Runtime.CompilerServices;

namespace Yaaw.API.Hubs;

[Authorize]
internal sealed class ChatHub : Hub
{
    public async IAsyncEnumerable<ClientMessageFragmentDto> Stream(
        Guid id,
        StreamContext context,
        IChatStreamingCoordinator coordinator,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in coordinator.StreamFragments(id, context.LastMessageId, context.LastFragmentId, cancellationToken))
        {
            yield return message;
        }
    }
}
