using MediatR;
using Microsoft.EntityFrameworkCore;
using Yaaw.Application.DTOs.Conversations;
using Yaaw.Application.Sorting;
using Yaaw.Domain.Entities;
using Yaaw.Domain.Interfaces;

namespace Yaaw.Application.Conversations.Queries;

public sealed record GetConversationsQuery(string UserId, string? Search, string? Sort) : IRequest<List<ConversationDto>>;

internal sealed class GetConversationsHandler(
    IConversationRepository repository,
    SortMappingProvider sortMappingProvider)
    : IRequestHandler<GetConversationsQuery, List<ConversationDto>>
{
    public async Task<List<ConversationDto>> Handle(GetConversationsQuery request, CancellationToken ct)
    {
        SortMapping[] sortMappings = sortMappingProvider.GetMappings<Conversation, ConversationDto>();

        IQueryable<Conversation> query = await repository.GetQueryableByUserIdAsync(request.UserId, ct);

        string? search = request.Search?.Trim().ToLower();

        return await query
            .Where(c => search == null || c.Name.ToLower().Contains(search))
            .Include(c => c.Messages)
            .ApplySort(request.Sort, sortMappings, defaultOrderBy: nameof(Conversation.Id))
            .Select(c => c.ToDto())
            .ToListAsync(ct);
    }
}
