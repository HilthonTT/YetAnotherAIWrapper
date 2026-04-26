using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Yaaw.API.Database;
using Yaaw.API.DTOs.Common;
using Yaaw.API.DTOs.Conversations;
using Yaaw.API.DTOs.Messages;
using Yaaw.API.Entities;
using Yaaw.API.Hubs;
using Yaaw.API.Services;

namespace Yaaw.API.Endpoints;

public static class ChatEndpoints
{
    public static WebApplication MapChatApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/chat");

        group.MapHub<ChatHub>("/stream", o => o.AllowStatefulReconnects = true);

        group.MapGet("/", async (
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            List<Conversation> conversations = await dbContext.Conversations
                .Include(c => c.Messages)
                .ToListAsync(cancellationToken);

            List<ConversationDto> mappedConversations = conversations.Select(c => c.ToDto()).ToList();
            var response = new CollectionResponse<ConversationDto>()
            {
                Items = mappedConversations,
            };

            return Results.Ok(response);
        })
        .WithTags(Tags.Chat);

        group.MapGet("/{id:guid}", async (
            Guid id,
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            Conversation? conversation = await dbContext.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (conversation is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(conversation.ToDto());
        })
        .WithTags(Tags.Chat);

        group.MapPost("/", async (
            AppDbContext dbContext,
            NewConversationDto newConversationDto,
            IValidator<NewConversationDto> validator,
            CancellationToken cancellationToken) =>
        {
            await validator.ValidateAndThrowAsync(newConversationDto, cancellationToken);

            var conversation = new Conversation
            {
                Id = Guid.CreateVersion7(),
                Name = newConversationDto.Name,
                Messages = [],
            };

            await dbContext.Conversations.AddAsync(conversation, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/chat/{conversation.Id}", conversation.ToDto());
        })
        .WithTags(Tags.Chat);

        group.MapPost("/{id:guid}", async (
            Guid id,
            PromptDto promptDto,
            ChatStreamingCoordinator streamingCoordinator,
            IValidator<PromptDto> validator,
            CancellationToken cancellationToken) =>
        {
            await validator.ValidateAndThrowAsync(promptDto, cancellationToken);

            await streamingCoordinator.AddStreamingMessage(id, promptDto.Text);

            return Results.Ok();
        })
        .WithTags(Tags.Chat);

        group.MapPut("/{id:guid}/cancel", async (
            Guid id, 
            RedisCancellationManager cancellationManager) =>
        {
            await cancellationManager.CancelAsync(id);
            return Results.Ok();
        })
        .WithTags(Tags.Chat);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            Conversation? conversation = await dbContext.Conversations
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (conversation is null)
            {
                return Results.NotFound();
            }

            dbContext.Conversations.Remove(conversation);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .WithTags(Tags.Chat);

        return app;
    }
}
