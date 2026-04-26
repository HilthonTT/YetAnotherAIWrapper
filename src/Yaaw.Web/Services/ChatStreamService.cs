using Microsoft.AspNetCore.SignalR.Client;
using System.Runtime.CompilerServices;
using Yaaw.Web.Models;

namespace Yaaw.Web.Services;

public sealed class ChatStreamService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly string _hubUrl;

    public ChatStreamService(IConfiguration configuration)
    {
        // Aspire injects project URLs under this key pattern.
        string? baseUrl = configuration["services:yaaw-api:https:0"]
            ?? configuration["services:yaaw-api:http:0"]
            ?? configuration["ApiBaseUrl"];

        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new InvalidOperationException(
                "Cannot resolve yaaw-api URL. Ensure the project reference is configured in the AppHost or set ApiBaseUrl.");
        }

        _hubUrl = $"{baseUrl.TrimEnd('/')}/api/chat/stream";
    }

    public async Task EnsureConnectedAsync(CancellationToken ct = default)
    {
        if (_connection is { State: HubConnectionState.Connected })
        {
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();

        await _connection.StartAsync(ct);
    }

    public async IAsyncEnumerable<ClientMessageFragmentDto> StreamAsync(
        Guid conversationId,
        Guid? lastMessageId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);

        if (_connection is null)
        {
            yield break;
        }

        var context = new StreamContext(lastMessageId);

        var stream = _connection.StreamAsync<ClientMessageFragmentDto>(
            "Stream", conversationId, context, ct);

        await foreach (var fragment in stream.WithCancellation(ct))
        {
            yield return fragment;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
