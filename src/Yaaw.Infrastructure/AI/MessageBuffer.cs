using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;
using Yaaw.Application.DTOs.Messages;

namespace Yaaw.Infrastructure.AI;

internal sealed class MessageBuffer : IAsyncDisposable
{
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;
    private readonly ILogger<MessageBuffer> _logger;
    private readonly Guid _conversationId;
    private readonly ConcurrentQueue<ClientMessageFragmentDto> _buffer = [];
    private int _draining;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private const int MaxBufferSize = 20;
    private const int MaxBufferTimeMs = 500;

    private int _count;
    private readonly PeriodicTimer _flushTimer;
    private readonly Task _timerTask;
    private readonly CancellationTokenSource _timerCts = new();

    public MessageBuffer(IDatabase database, ISubscriber subscriber, ILogger<MessageBuffer> logger, Guid conversationId)
    {
        _database = database;
        _subscriber = subscriber;
        _logger = logger;
        _conversationId = conversationId;
        _flushTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(MaxBufferTimeMs));
        _timerTask = RunTimerLoopAsync(_timerCts.Token);
    }

    private async Task RunTimerLoopAsync(CancellationToken ct)
    {
        try
        {
            while (await _flushTimer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                if (Volatile.Read(ref _draining) == 1)
                    break;

                await TriggerFlushAsync(waitForLock: false).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in flush timer loop for conversation {ConversationId}", _conversationId);
        }
    }

    public async Task AddFragmentAsync(ClientMessageFragmentDto fragment)
    {
        if (Volatile.Read(ref _draining) == 1)
        {
            throw new InvalidOperationException("Cannot add fragments while draining.");
        }

        _buffer.Enqueue(fragment);
        int newCount = Interlocked.Increment(ref _count);

        if (newCount >= MaxBufferSize || fragment.IsFinal)
        {
            await TriggerFlushAsync(waitForLock: false).ConfigureAwait(false);
        }
    }

    private async Task TriggerFlushAsync(bool waitForLock)
    {
        bool acquired = waitForLock
            ? await _flushLock.WaitAsync(Timeout.Infinite).ConfigureAwait(false)
            : await _flushLock.WaitAsync(0).ConfigureAwait(false);

        if (!acquired)
        {
            return;
        }

        try
        {
            await FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private async Task FlushAsync()
    {
        var fragmentsToFlush = new List<ClientMessageFragmentDto>();

        while (_buffer.TryDequeue(out var fragment))
        {
            fragmentsToFlush.Add(fragment);
            Interlocked.Decrement(ref _count);
        }

        if (fragmentsToFlush.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "Flushing {Count} fragments for conversation {ConversationId} (first id: {MessageId})",
            fragmentsToFlush.Count, _conversationId, fragmentsToFlush[0].Id);

        var key = RedisKeys.GetBacklogKey(_conversationId);
        var channel = RedisKeys.GetRedisChannelName(_conversationId);
        var coalesced = CoalesceFragments(fragmentsToFlush);
        string serialized = JsonSerializer.Serialize(coalesced);

        try
        {
            await Task.WhenAll(
                _database.ListRightPushAsync(key, serialized),
                _subscriber.PublishAsync(channel, serialized)
            ).ConfigureAwait(false);

            _logger.LogInformation(
                "Flushed {Count} fragments for conversation {ConversationId}",
                fragmentsToFlush.Count, _conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing fragments for conversation {ConversationId}. Re-enqueueing {Count} fragments.",
                _conversationId, fragmentsToFlush.Count);

            var surviving = new ConcurrentQueue<ClientMessageFragmentDto>(fragmentsToFlush);
            while (_buffer.TryDequeue(out var lateFragment))
            {
                surviving.Enqueue(lateFragment);
                Interlocked.Decrement(ref _count);
            }
            while (surviving.TryDequeue(out var f))
            {
                _buffer.Enqueue(f);
                Interlocked.Increment(ref _count);
            }

            throw;
        }
    }

    private static ClientMessageFragmentDto CoalesceFragments(List<ClientMessageFragmentDto> fragments)
    {
        var lastFragment = fragments[^1];
        int totalLength = 0;

        for (int i = 0; i < fragments.Count; i++)
        {
            totalLength += fragments[i].Text.Length;
        }

        string combined = string.Create(totalLength, fragments, static (span, frags) =>
        {
            int pos = 0;
            for (int i = 0; i < frags.Count; i++)
            {
                ReadOnlySpan<char> text = frags[i].Text.AsSpan();
                text.CopyTo(span[pos..]);
                pos += text.Length;
            }
        });

        return new ClientMessageFragmentDto(
            lastFragment.Id,
            lastFragment.Sender,
            combined,
            lastFragment.FragmentId,
            lastFragment.IsFinal);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _draining, 1) != 0)
        {
            return;
        }

        await _timerCts.CancelAsync().ConfigureAwait(false);
        await _timerTask.ConfigureAwait(false);
        _flushTimer.Dispose();
        _timerCts.Dispose();

        try
        {
            await TriggerFlushAsync(waitForLock: true).ConfigureAwait(false);
        }
        finally
        {
            _flushLock.Dispose();
        }
    }
}
