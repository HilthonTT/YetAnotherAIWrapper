using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Yaaw.API.DTOs.Messages;

namespace Yaaw.API.Services;

public sealed class RedisConversationState : IAsyncDisposable
{
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;
    private readonly ILogger<RedisConversationState> _logger;
    private readonly ILoggerFactory _loggerFactory;

    // Non-static: injected as singleton so the registry lifetime is explicit.
    private readonly ConcurrentDictionary<Guid, List<Action<ClientMessageFragmentDto>>> _localSubscribers = new();

    private readonly RedisChannel _patternChannel;

    // conversationId → buffer (key is consistent throughout).
    private readonly ConcurrentDictionary<Guid, MessageBuffer> _messageBuffers = new();

    public RedisConversationState(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisConversationState> logger,
        ILoggerFactory loggerFactory)
    {
        _database = connectionMultiplexer.GetDatabase();
        _subscriber = connectionMultiplexer.GetSubscriber();
        _logger = logger;
        _loggerFactory = loggerFactory;
        _patternChannel = RedisChannel.Pattern("conversation:*:channel");
    }

    // Call once at startup (e.g. from IHostedService or middleware).
    public async Task StartAsync()
    {
        await _subscriber.SubscribeAsync(_patternChannel, OnRedisMessage);
        _logger.LogInformation("Subscribed to pattern {Pattern}", _patternChannel);
    }

    private void OnRedisMessage(RedisChannel channel, RedisValue value)
    {
        // Extract conversationId from "conversation:{id}:channel".
        var channelStr = channel.ToString();
        var parts = channelStr.Split(':');
        if (parts.Length < 3 || !Guid.TryParse(parts[1], out var conversationId))
        {
            _logger.LogWarning("Received message on unexpected channel {Channel}", channelStr);
            return;
        }

        ClientMessageFragmentDto? fragment;
        try
        {
            fragment = JsonSerializer.Deserialize<ClientMessageFragmentDto>((ReadOnlySpan<byte>)value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize fragment on channel {Channel}", channelStr);
            return;
        }

        if (fragment is null)
        {
            return;
        }

        FanOut(conversationId, fragment);
    }

    private void FanOut(Guid conversationId, ClientMessageFragmentDto fragment)
    {
        if (!_localSubscribers.TryGetValue(conversationId, out var list))
        {
            return;
        }

        Action<ClientMessageFragmentDto>[] snapshot;
        lock (list)
        {
            snapshot = [.. list];
        }

        foreach (var callback in snapshot)
        {
            try 
            { 
                callback(fragment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Local subscriber threw for conversation {ConversationId}", conversationId);
            }
        }
    }

    public async IAsyncEnumerable<ClientMessageFragmentDto> Subscribe(
        Guid conversationId,
        Guid? lastMessageId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "New subscription for conversation {ConversationId}, resuming after {LastMessageId}",
            conversationId, lastMessageId);

        var channel = Channel.CreateUnbounded<ClientMessageFragmentDto>();

        // Register BEFORE backlog fetch to avoid missing messages that arrive
        // between the two. Duplicates are filtered by the highWaterMark below.
        Guid? highWaterMark = lastMessageId;

        void LocalCallback(ClientMessageFragmentDto fragment)
        {
            // Only enqueue IDs strictly greater than the highest ID seen so far.
            // highWaterMark is updated after the backlog is consumed.
            if (highWaterMark is not null && fragment.Id <= highWaterMark)
            {
                return;
            }

            _logger.LogDebug("Fan-out fragment {FragmentId} for {ConversationId}", fragment.Id, conversationId);
            channel.Writer.TryWrite(fragment);
        }

        AddLocalSubscriber(conversationId, LocalCallback);

        try
        {
            var key = RedisKeys.GetBacklogKey(conversationId);
            var values = await _database.ListRangeAsync(key);

            Guid? maxBacklogId = null;
            for (int i = 0; i < values.Length; i++)
            {
                var fragment = JsonSerializer.Deserialize<ClientMessageFragmentDto>((ReadOnlySpan<byte>)values[i]!);
                if (fragment is null)
                {
                    continue;
                }

                if (lastMessageId is null || fragment.Id > lastMessageId)
                {
                    yield return fragment;
                    if (maxBacklogId is null || fragment.Id > maxBacklogId)
                    {
                        maxBacklogId = fragment.Id;
                    }
                }
            }

            // Advance the high-water mark so the callback deduplicates correctly.
            if (maxBacklogId is not null)
                highWaterMark = maxBacklogId;

            using var reg = cancellationToken.UnsafeRegister(
                static s => ((ChannelWriter<ClientMessageFragmentDto>)s!).TryComplete(),
                channel.Writer);

            await foreach (var fragment in channel.Reader.ReadAllAsync(CancellationToken.None))
            {
                yield return fragment;
            }
        }
        finally
        {
            RemoveLocalSubscriber(conversationId, LocalCallback);
            _logger.LogInformation("Subscription for conversation {ConversationId} ended", conversationId);
        }
    }

    // Adds a fragment to the Nagle buffer for the given conversation.
    public async Task PublishFragmentAsync(Guid conversationId, ClientMessageFragmentDto fragment)
    {
        var bufferLogger = _loggerFactory.CreateLogger<MessageBuffer>();
        var buffer = _messageBuffers.GetOrAdd(
            conversationId,
            id => new MessageBuffer(_database, _subscriber, bufferLogger, id));

        await buffer.AddFragmentAsync(fragment);
    }

    public async Task<List<ClientMessageDto>> GetUnpublishedMessagesAsync(Guid conversationId)
    {
        var key = RedisKeys.GetBacklogKey(conversationId);
        var values = await _database.ListRangeAsync(key);
        var fragments = new List<ClientMessageFragmentDto>(values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            var fragment = JsonSerializer.Deserialize<ClientMessageFragmentDto>((ReadOnlySpan<byte>)values[i]!);
            if (fragment is not null)
                fragments.Add(fragment);
        }

        var messages = new List<ClientMessageDto>();

        foreach (var g in fragments.GroupBy(f => f.Id))
        {
            // The first fragment is the placeholder "Generating reply..." text;
            // skip it so only real content is coalesced.
            const int PlaceholderFragments = 1;
            var realFragments = g.Skip(PlaceholderFragments).ToList();

            if (realFragments.Count == 0)
            {
                continue;
            }

            var coalesced = ClientMessageFragmentDto.CoalesceFragments(realFragments);
            messages.Add(new ClientMessageDto(coalesced.Id, coalesced.Sender, coalesced.Text));
        }

        return messages;
    }

    public async Task CompleteAsync(Guid conversationId, Guid messageId)
    {
        // Key is conversationId, matching how PublishFragmentAsync inserts it.
        if (_messageBuffers.TryRemove(conversationId, out var buffer))
        {
            await buffer.DisposeAsync();
        }

        await _database.KeyExpireAsync(
            RedisKeys.GetBacklogKey(conversationId),
            TimeSpan.FromMinutes(5));
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _subscriber.UnsubscribeAsync(_patternChannel);
            _logger.LogInformation("Unsubscribed from pattern {Pattern}", _patternChannel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during unsubscription for pattern {Pattern}", _patternChannel);
        }

        // Drain all active buffers.
        foreach (var (_, buffer) in _messageBuffers)
        {
            await buffer.DisposeAsync();
        }
        _messageBuffers.Clear();

        // Clear local subscriber registry.
        _localSubscribers.Clear();
    }

    private void AddLocalSubscriber(Guid conversationId, Action<ClientMessageFragmentDto> callback)
    {
        var list = _localSubscribers.GetOrAdd(conversationId, _ => []);
        lock (list)
        {
            list.Add(callback);
        }
    }

    private void RemoveLocalSubscriber(Guid conversationId, Action<ClientMessageFragmentDto> callback)
    {
        if (!_localSubscribers.TryGetValue(conversationId, out var list))
        {
            return;
        }

        lock (list)
        {
            list.Remove(callback);
            if (list.Count == 0)
            {
                _localSubscribers.TryRemove(conversationId, out _);
            }
        }
    }
}