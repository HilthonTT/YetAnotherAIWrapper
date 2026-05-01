using System.Dynamic;
using MediatR;
using Yaaw.Application.Conversations.Commands;
using Yaaw.Application.Conversations.Queries;
using Yaaw.Application.DTOs.Common;
using Yaaw.Application.DTOs.Conversations;
using Yaaw.Application.Interfaces;
using Yaaw.Application.Services;
using Yaaw.Application.Sorting;
using Yaaw.API.Hubs;
using Yaaw.API.Middleware;
using Yaaw.API.Services;
using Yaaw.Domain.Entities;

namespace Yaaw.API.Endpoints;

internal static class ChatEndpoints
{
    public static WebApplication MapChatApi(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/chat")
            .RequireAuthorization();

        group.MapHub<ChatHub>("/stream", o => o.AllowStatefulReconnects = true);

        group.MapGet("/", GetConversations)
         .WithName(nameof(GetConversations))
         .WithSummary("List conversations")
         .WithDescription("Returns all conversations with optional sorting and field selection.")
         .Produces<CollectionResponse<ConversationDto>>()
         .ProducesProblem(StatusCodes.Status400BadRequest)
         .AddEndpointFilter<ETagCachingFilter>();

        group.MapGet("/{id:guid}", GetConversation)
            .WithName(nameof(GetConversation))
            .WithSummary("Get conversation")
            .WithDescription("Returns a single conversation by ID, including its messages.")
            .Produces<ConversationDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter<ETagCachingFilter>();

        group.MapPost("/", CreateConversation)
            .WithName(nameof(CreateConversation))
            .WithSummary("Create conversation")
            .WithDescription("Creates a new empty conversation.")
            .Produces<ConversationDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .AddEndpointFilter<CacheInvalidationFilter>();

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
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter<CacheInvalidationFilter>();

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
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter<CacheInvalidationFilter>();

        return app;
    }

    private static async Task<IResult> GetConversations(
        [AsParameters] ConversationQueryParameters query,
        ISender mediator,
        LinkService linkService,
        SortMappingProvider sortMappingProvider,
        DataShapingService dataShapingService,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        string userId = currentUser.GetUserId();

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

        List<ConversationDto> conversations = await mediator.Send(
            new GetConversationsQuery(userId, query.Search, query.Sort), cancellationToken);

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
        ISender mediator,
        LinkService linkService,
        DataShapingService dataShapingService,
        ICurrentUserService currentUser,
        string? fields,
        CancellationToken cancellationToken)
    {
        string userId = currentUser.GetUserId();

        if (!dataShapingService.Validate<ConversationDto>(fields))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"Invalid field(s) in '{fields}'.");
        }

        ConversationDto? dto = await mediator.Send(
            new GetConversationQuery(id, userId), cancellationToken);

        if (dto is null)
        {
            return Results.NotFound();
        }

        ExpandoObject shaped = dataShapingService.ShapeData(dto, fields);
        ((IDictionary<string, object?>)shaped)["links"] = CreateConversationLinks(linkService, id);

        return Results.Ok(shaped);
    }

    private static async Task<IResult> CreateConversation(
        NewConversationDto newConversationDto,
        ISender mediator,
        LinkService linkService,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        string userId = currentUser.GetUserId();

        ConversationDto dto = await mediator.Send(
            new CreateConversationCommand(newConversationDto.Name, userId), cancellationToken);

        return Results.Created(
            linkService.Create(nameof(GetConversation), "self", HttpMethods.Get, new { id = dto.Id }).Href,
            dto);
    }

    private static async Task<IResult> SendPrompt(
        Guid id,
        Application.DTOs.Messages.PromptDto promptDto,
        ISender mediator,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        string userId = currentUser.GetUserId();

        bool result = await mediator.Send(
            new SendPromptCommand(id, userId, promptDto.Text), cancellationToken);

        return result ? Results.Accepted() : Results.NotFound();
    }

    private static async Task<IResult> RenameConversation(
        Guid id,
        RenameConversationDto renameDto,
        ISender mediator,
        LinkService linkService,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        string userId = currentUser.GetUserId();

        ConversationDto? dto = await mediator.Send(
            new RenameConversationCommand(id, userId, renameDto.Name), cancellationToken);

        return dto is null
            ? Results.NotFound()
            : Results.Ok(dto);
    }

    private static async Task<IResult> CancelStream(
        Guid id,
        ISender mediator,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        string userId = currentUser.GetUserId();

        bool result = await mediator.Send(
            new CancelStreamCommand(id, userId), cancellationToken);

        return result ? Results.Ok() : Results.NotFound();
    }

    private static async Task<IResult> DeleteConversation(
        Guid id,
        ISender mediator,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        string userId = currentUser.GetUserId();

        bool deleted = await mediator.Send(
            new DeleteConversationCommand(id, userId), cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
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
