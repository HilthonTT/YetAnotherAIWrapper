using Yaaw.API.Entities;
using Yaaw.API.Services.Sorting;

namespace Yaaw.API.DTOs.Conversations;

internal static class ConversationMappings
{
    public static readonly SortMappingDefinition<Conversation, ConversationDto> SortMappings = new()
    {
        Mappings =
        [
            new SortMapping("id", nameof(ConversationDto.Id)),
            new SortMapping("name", nameof(ConversationDto.Name)),
        ],
    };

    internal static ConversationDto ToDto(this Conversation conversation)
    {
        return new ConversationDto
        {
            Id = conversation.Id,
            Name = conversation.Name,
            Messages = conversation.Messages.Select(m => m.ToDto()).ToList(),
        };
    }
}
