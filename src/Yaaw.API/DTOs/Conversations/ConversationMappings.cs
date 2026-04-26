using Yaaw.API.Entities;

namespace Yaaw.API.DTOs.Conversations;

internal static class ConversationMappings
{
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
