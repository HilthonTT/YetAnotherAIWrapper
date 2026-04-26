using Yaaw.API.Entities;

namespace Yaaw.API.DTOs.Messages;

internal static class ClientMessageDtoMappings
{
    internal static ConversationMessageDto ToDto(this ConversationMessage message)
    {
        return new ConversationMessageDto
        {
           Id = message.Id,
           ConversationId = message.ConversationId,
           Role = message.Role,
           Text = message.Text,
        };
    }
}
