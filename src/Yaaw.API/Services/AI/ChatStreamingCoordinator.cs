using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using Yaaw.API.Database;
using Yaaw.API.DTOs.Messages;
using Yaaw.API.Entities;
using System.Runtime.CompilerServices;

namespace Yaaw.API.Services.AI;

internal sealed class ChatStreamingCoordinator(
    IChatClient chatClient,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ChatStreamingCoordinator> logger,
    RedisConversationState conversationState,
    RedisCancellationManager cancellationManager)
{
    // TODO: Read from configuration
    private static readonly TimeSpan DefaultStreamItemTimeout = TimeSpan.FromMinutes(1);

    public async Task AddStreamingMessage(Guid conversationId, string text)
    {
        List<ChatMessage> messages = await SavePromptAndGetMessageHistoryAsync(conversationId, text);

        if (messages.Count == 0)
        {
            logger.LogWarning("No messages for {ConversationId}, skipping streaming", conversationId);
            return;
        }

        _ = Task.Run(() => StreamReplyAsync(conversationId, messages))
            .ContinueWith(
                t => logger.LogCritical(t.Exception, "Unhandled fault in StreamReplyAsync for {ConversationId}", conversationId),
                TaskContinuationOptions.OnlyOnFaulted);
    }

    public async IAsyncEnumerable<ClientMessageFragmentDto> StreamFragments(
        Guid conversationId, 
        Guid? lastMessageId,
        Guid? lastDeliveredFragment,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Getting message stream for conversation {ConversationId}, {LastMessageId}", 
                conversationId, 
                lastMessageId);
        }

        var stream = conversationState.Subscribe(conversationId, lastMessageId, cancellationToken);

        await foreach (var fragment in stream.WithCancellation(cancellationToken))
        {
            // Use lastMessageId to filter out fragments from an already delivered message,
            // while using lastDeliveredFragment (a sortable GUID) for ordering and de-duping.
            if (lastDeliveredFragment is null || fragment.FragmentId > lastDeliveredFragment)
            {
                lastDeliveredFragment = fragment.FragmentId;
            }
            else
            {
                continue;
            }

            yield return fragment;
        }
    }

    private async Task StreamReplyAsync(Guid conversationId, List<ChatMessage> messages)
    {
        var assistantReplyId = Guid.CreateVersion7();

        logger.LogInformation("Streaming reply for {ConversationId} {MessageId}", conversationId, assistantReplyId);

        var allChunks = new List<ChatResponseUpdate>();
        var token = cancellationManager.Register(assistantReplyId);

        // Publish the placeholder fragment.
        await conversationState.PublishFragmentAsync(
            conversationId,
            new ClientMessageFragmentDto(assistantReplyId, ChatRole.Assistant.Value, "Generating reply...", Guid.CreateVersion7()));

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(DefaultStreamItemTimeout);

            await foreach (var update in chatClient.GetStreamingResponseAsync(messages).WithCancellation(cts.Token))
            {
                cts.CancelAfter(DefaultStreamItemTimeout);

                if (!string.IsNullOrWhiteSpace(update.Text))
                {
                    allChunks.Add(update);
                    await conversationState.PublishFragmentAsync(
                        conversationId,
                        new ClientMessageFragmentDto(assistantReplyId, ChatRole.Assistant.Value, update.Text, Guid.CreateVersion7()));
                }
            }

            logger.LogInformation("Streaming complete for {ConversationId} {MessageId}", conversationId, assistantReplyId);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Streaming cancelled for {ConversationId} {MessageId}", conversationId, assistantReplyId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Streaming error for {ConversationId} {MessageId}", conversationId, assistantReplyId);

            await TryAsync(() => conversationState.PublishFragmentAsync(
                conversationId,
                new ClientMessageFragmentDto(assistantReplyId, ChatRole.Assistant.Value, "Error streaming message", Guid.CreateVersion7())));
        }
        finally
        {
            // Persist whatever was collected, even if cancelled or errored.
            string? fullText = allChunks.Count > 0 ? allChunks.ToChatResponse().Text : null;
            if (fullText is not null)
            {
                await TryAsync(() => SaveAssistantMessageAsync(conversationId, assistantReplyId, fullText));
            }

            // Always publish the final fragment, then clean up Redis — regardless of DB outcome.
            await TryAsync(() => conversationState.PublishFragmentAsync(
                conversationId,
                new ClientMessageFragmentDto(assistantReplyId, ChatRole.Assistant.Value, "", Guid.CreateVersion7(), IsFinal: true)));

            await TryAsync(() => conversationState.CompleteAsync(conversationId, assistantReplyId));

            cancellationManager.Unregister(assistantReplyId);
        }
    }

    private async Task TryAsync(Func<Task> action, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        try
        {
            await action();
        }
        catch (Exception ex) 
        {
            logger.LogError(ex, "Cleanup step failed in {Caller}", caller);
        }
    }

    private async Task<List<ChatMessage>> SavePromptAndGetMessageHistoryAsync(Guid conversationId, string text)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        bool exists = await dbContext.Conversations.AnyAsync(c => c.Id == conversationId);
        if (!exists)
        {
            return [];
        }

        var userMessage = new ConversationMessage
        {
            Id = Guid.CreateVersion7(),
            ConversationId = conversationId,
            Role = ChatRole.User.Value,
            Text = text,
        };

        dbContext.Add(userMessage);
        await dbContext.SaveChangesAsync();

        await conversationState.PublishFragmentAsync(
            conversationId,
            new ClientMessageFragmentDto(userMessage.Id, ChatRole.User.Value, text, Guid.CreateVersion7(), IsFinal: true));

        List<ConversationMessage> history = await dbContext.ConversationMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.Id)
            .ToListAsync();

        return history
            .Select(m => new ChatMessage(new(m.Role), m.Text))
            .ToList();
    }

    private async Task SaveAssistantMessageAsync(Guid conversationId, Guid messageId, string text)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        bool exists = await dbContext.Conversations.AnyAsync(c => c.Id == conversationId);
        if (!exists)
        {
            logger.LogWarning("Conversation {ConversationId} not found when saving assistant message {MessageId}", conversationId, messageId);
            return;
        }

        dbContext.Add(new ConversationMessage
        {
            Id = messageId,
            ConversationId = conversationId,
            Role = ChatRole.Assistant.Value,
            Text = text,
        });

        await dbContext.SaveChangesAsync();
    }
}
