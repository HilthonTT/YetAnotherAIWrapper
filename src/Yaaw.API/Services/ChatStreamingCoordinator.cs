using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using Yaaw.API.Database;
using Yaaw.API.DTOs.Messages;
using Yaaw.API.Entities;

namespace Yaaw.API.Services;

public sealed class ChatStreamingCoordinator(
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
        var messages = await SavePromptAndGetMessageHistoryAsync(conversationId, text);

        _ = Task.Run(() => StreamReplyAsync(conversationId, messages))
            .ContinueWith(
                t => logger.LogCritical(t.Exception, "Unhandled fault in StreamReplyAsync for {ConversationId}", conversationId),
                TaskContinuationOptions.OnlyOnFaulted);
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

        // Include Messages so the history is actually populated.
        var conversation = await dbContext.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation is null)
        {
            return [];
        }

        var userMessage = new ConversationMessage
        {
            Id = Guid.CreateVersion7(),
            ConversationId = conversation.Id,
            Role = ChatRole.User.Value,
            Text = text,
        };

        conversation.Messages.Add(userMessage);
        await dbContext.SaveChangesAsync();

        // Publish the user's fragment so subscribers see it immediately.
        await conversationState.PublishFragmentAsync(
            conversationId,
            new ClientMessageFragmentDto(userMessage.Id, ChatRole.User.Value, text, Guid.CreateVersion7(), IsFinal: true));

        return conversation.Messages
            .Select(m => new ChatMessage(new(m.Role), m.Text))
            .ToList();
    }

    private async Task SaveAssistantMessageAsync(Guid conversationId, Guid messageId, string text)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conversation = await dbContext.Conversations.FindAsync(conversationId);
        if (conversation is null)
        {
            logger.LogWarning("Conversation {ConversationId} not found when saving assistant message {MessageId}", conversationId, messageId);
            return;
        }

        conversation.Messages.Add(new ConversationMessage
        {
            Id = messageId,
            ConversationId = conversation.Id,
            Role = ChatRole.Assistant.Value,
            Text = text,
        });

        await dbContext.SaveChangesAsync();
    }
}
