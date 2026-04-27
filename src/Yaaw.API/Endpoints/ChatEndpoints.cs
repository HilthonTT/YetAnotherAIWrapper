using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System.Dynamic;
using Yaaw.API.Database;
using Yaaw.API.DTOs.Common;
using Yaaw.API.DTOs.Conversations;
using Yaaw.API.DTOs.Messages;
using Yaaw.API.Entities;
using Yaaw.API.Hubs;
using Yaaw.API.Services;
using Yaaw.API.Services.Sorting;

namespace Yaaw.API.Endpoints;

internal static class ChatEndpoints
{
    public static WebApplication MapChatApi(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/chat");

        group.MapHub<ChatHub>("/stream", o => o.AllowStatefulReconnects = true);

        group.MapGet("/", GetConversations)
         .WithName(nameof(GetConversations))
         .WithSummary("List conversations")
         .WithDescription("Returns all conversations with optional sorting and field selection.")
         .Produces<CollectionResponse<ConversationDto>>()
         .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetConversation)
            .WithName(nameof(GetConversation))
            .WithSummary("Get conversation")
            .WithDescription("Returns a single conversation by ID, including its messages.")
            .Produces<ConversationDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateConversation)
            .WithName(nameof(CreateConversation))
            .WithSummary("Create conversation")
            .WithDescription("Creates a new empty conversation.")
            .Produces<ConversationDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPost("/{id:guid}", SendPrompt)
            .WithName(nameof(SendPrompt))
            .WithSummary("Send prompt")
            .WithDescription("Queues a user prompt for AI streaming via the SignalR hub.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem();

        group.MapPatch("/{id:guid}", RenameConversation)
            .WithName(nameof(RenameConversation))
            .WithSummary("Rename conversation")
            .WithDescription("Updates the display name of an existing conversation.")
            .Produces<ConversationDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/cancel", CancelStream)
            .WithName(nameof(CancelStream))
            .WithSummary("Cancel active stream")
            .WithDescription("Signals cancellation for an in-progress AI streaming response.")
            .Produces(StatusCodes.Status200OK);

        group.MapDelete("/{id:guid}", DeleteConversation)
            .WithName(nameof(DeleteConversation))
            .WithSummary("Delete conversation")
            .WithDescription("Permanently deletes a conversation and all its messages.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetConversations(
        [AsParameters] ConversationQueryParameters query,
        AppDbContext dbContext,
        LinkService linkService,
        SortMappingProvider sortMappingProvider,
        DataShapingService dataShapingService,
        CancellationToken cancellationToken)
    {
        SortMapping[] sortMappings = sortMappingProvider
            .GetMappings<Conversation, ConversationDto>();

        if (!sortMappingProvider.ValidateMappings<Conversation, ConversationDto>(query.Sort))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"Invalid sort field(s) in '{query.Sort}'.");
        }

        if (!dataShapingService.Validate<ConversationDto>(query.Fields))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"Invalid field(s) in '{query.Fields}'.");
        }

        string? search = query.Search?.Trim().ToLower();

        List<ConversationDto> conversations = await dbContext.Conversations
            .Where(c => search == null || c.Name.ToLower().Contains(search))
            .Include(c => c.Messages)
            .ApplySort(query.Sort, sortMappings, defaultOrderBy: nameof(Conversation.Id))
            .Select(c => c.ToDto())
            .ToListAsync(cancellationToken);

        List<ExpandoObject> shaped = dataShapingService.ShapeCollectionData(
            conversations,
            query.Fields,
            dto => CreateConversationLinks(linkService, dto.Id));

        List<LinkDto> collectionLinks =
        [
            linkService.Create(nameof(GetConversations), "self", HttpMethods.Get),
            linkService.Create(nameof(CreateConversation), "create-conversation", HttpMethods.Post),
        ];

        return Results.Ok(new { items = shaped, links = collectionLinks });
    }

    private static async Task<IResult> GetConversation(
        Guid id,
        AppDbContext dbContext,
        LinkService linkService,
        DataShapingService dataShapingService,
        string? fields,
        CancellationToken cancellationToken)
    {
        if (!dataShapingService.Validate<ConversationDto>(fields))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"Invalid field(s) in '{fields}'.");
        }

        Conversation? conversation = await dbContext.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (conversation is null)
        {
            return Results.NotFound();
        }

        ConversationDto dto = conversation.ToDto();
        ExpandoObject shaped = dataShapingService.ShapeData(dto, fields);

        ((IDictionary<string, object?>)shaped)["links"] = CreateConversationLinks(linkService, id);

        return Results.Ok(shaped);
    }

    private static async Task<IResult> CreateConversation(
        NewConversationDto newConversationDto,
        AppDbContext dbContext,
        LinkService linkService,
        IValidator<NewConversationDto> validator,
        CancellationToken cancellationToken)
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

        ConversationDto dto = conversation.ToDto();

        return Results.Created(
            linkService.Create(nameof(GetConversation), "self", HttpMethods.Get, new { id = conversation.Id }).Href,
            dto);
    }

    private static async Task<IResult> SendPrompt(
        Guid id,
        PromptDto promptDto,
        ChatStreamingCoordinator streamingCoordinator,
        IValidator<PromptDto> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(promptDto, cancellationToken);

        await streamingCoordinator.AddStreamingMessage(id, promptDto.Text);

        return Results.Accepted();
    }

    private static async Task<IResult> RenameConversation(
        Guid id,
        RenameConversationDto renameDto,
        AppDbContext dbContext,
        LinkService linkService,
        IValidator<RenameConversationDto> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(renameDto, cancellationToken);

        Conversation? conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (conversation is null)
        {
            return Results.NotFound();
        }

        conversation.Name = renameDto.Name;
        await dbContext.SaveChangesAsync(cancellationToken);

        ConversationDto dto = conversation.ToDto();

        return Results.Ok(dto);
    }

    private static async Task<IResult> CancelStream(
        Guid id,
        RedisCancellationManager cancellationManager)
    {
        await cancellationManager.CancelAsync(id);
        return Results.Ok();
    }

    private static async Task<IResult> DeleteConversation(
        Guid id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        int deleted = await dbContext.Conversations
            .Where(c => c.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static List<LinkDto> CreateConversationLinks(LinkService linkService, Guid id)
    {
        var values = new { id };

        return
        [
            linkService.Create(nameof(GetConversation), "self", HttpMethods.Get, values),
            linkService.Create(nameof(SendPrompt), "send-prompt", HttpMethods.Post, values),
            linkService.Create(nameof(RenameConversation), "rename", HttpMethods.Patch, values),
            linkService.Create(nameof(CancelStream), "cancel-stream", HttpMethods.Put, values),
            linkService.Create(nameof(DeleteConversation), "delete", HttpMethods.Delete, values),
        ];
    }
}