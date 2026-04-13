using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;
using Yaaw.API.DTOs.Messages;

namespace Yaaw.API.Services;

public sealed class MessageBuffer : IAsyncDisposable
{
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;
    private readonly ILogger<MessageBuffer> _logger;
    private readonly Guid _conversationId;
    private readonly ConcurrentQueue<ClientMessageFragmentDto> _buffer = [];
    // 0 = active, 1 = draining. Interlocked ensures atomicity.
    private int _draining;
    // SemaphoreSlim ensures only one FlushAsync runs at a time.
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
        // PeriodicTimer drives the loop; exceptions are observed on DisposeAsync.
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
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            // Timer loop failure is surfaced here rather than swallowed as a fire-and-forget.
            _logger.LogError(ex, "Unexpected error in flush timer loop for conversation {ConversationId}", _conversationId);
        }
    }

    public async Task AddFragmentAsync(ClientMessageFragmentDto fragment)
    {
        // Atomically check _draining before enqueuing.
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

    // waitForLock: true during dispose so the final flush is never skipped.
    private async Task TriggerFlushAsync(bool waitForLock)
    {
        bool acquired = waitForLock
            ? await _flushLock.WaitAsync(Timeout.Infinite).ConfigureAwait(false)
            : await _flushLock.WaitAsync(0).ConfigureAwait(false);

        if (!acquired)
        {
            return; // Another flush is in progress; skip (only valid for non-dispose callers).
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

            // Re-enqueue in original order by prepending into a temporary structure.
            // ConcurrentQueue doesn't support prepend, so we rebuild it from the
            // salvaged fragments followed by whatever arrived during the failed flush.
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
        // Guard against double-dispose.
        if (Interlocked.Exchange(ref _draining, 1) != 0)
        {
            return;
        }

        // Stop the timer loop cleanly before doing the final flush.
        await _timerCts.CancelAsync().ConfigureAwait(false);
        await _timerTask.ConfigureAwait(false);
        _flushTimer.Dispose();
        _timerCts.Dispose();

        // Final flush: waitForLock: true so we always complete it, even if another
        // flush was triggered by a racing AddFragmentAsync just before draining.
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
