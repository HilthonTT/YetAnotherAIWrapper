using Yaaw.Domain.Entities;

namespace Yaaw.Application.DTOs.Conversations;

internal static class ConversationMessageMappings
{
    internal static ConversationMessageDto ToDto(this ConversationMessage message)
    {
        return new ConversationMessageDto
        {
            Id = message.Id,
            Role = message.Role,
            ConversationId = message.ConversationId,
            Text = message.Text,
        };
    }
}
