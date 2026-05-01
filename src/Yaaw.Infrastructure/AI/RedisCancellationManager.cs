using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Collections.Concurrent;
using Yaaw.Application.Interfaces;

namespace Yaaw.Infrastructure.AI;

internal sealed class RedisCancellationManager : ICancellationManager, IAsyncDisposable
{
    private readonly ISubscriber _subscriber;
    private readonly ILogger<RedisCancellationManager> _logger;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _tokens = [];
    private readonly RedisChannel _channelName = RedisChannel.Literal("yaaw-cancellation");

    public RedisCancellationManager(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisCancellationManager> logger)
    {
        _subscriber = connectionMultiplexer.GetSubscriber()
            ?? throw new InvalidOperationException("Failed to get Redis subscriber instance.");

        _logger = logger;
        _subscriber.Subscribe(_channelName, OnCancellationMessage);

        _logger.LogInformation("Subscribed to cancellation channel {Channel}", _channelName);
    }

    private void OnCancellationMessage(RedisChannel channel, RedisValue message)
    {
        var raw = (string?)message;

        if (Guid.TryParse(raw, out Guid replyId))
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Received cancellation message for reply {ReplyId}", replyId);
            }

            if (_tokens.TryRemove(replyId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Cancelled token for reply {ReplyId}", replyId);
                }
            }
            else
            {
                _logger.LogWarning("No active token found for reply {ReplyId}", replyId);
            }
        }
        else
        {
            _logger.LogWarning("Received invalid cancellation message: {Message}", message);
        }
    }

    public CancellationToken Register(Guid id)
    {
        var cts = new CancellationTokenSource();
        _tokens[id] = cts;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Registered cancellation token for reply {ReplyId}", id);
        }

        return cts.Token;
    }

    public void Unregister(Guid id)
    {
        if (_tokens.TryRemove(id, out var cts))
        {
            cts.Dispose();

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Unregistered cancellation token for reply {ReplyId}", id);
            }
        }
    }

    public async Task CancelAsync(Guid id)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Publishing cancellation message for reply {ReplyId}", id);
        }
        await _subscriber.PublishAsync(_channelName, id.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _subscriber.UnsubscribeAsync(_channelName);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Unsubscribed from cancellation channel {Channel}", _channelName);
            }
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error unsubscribing from cancellation channel {Channel}", _channelName);
            }
        }

        foreach ((Guid _, CancellationTokenSource? cts) in _tokens)
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing cancellation token during shutdown");
            }
        }

        _tokens.Clear();
    }
}
