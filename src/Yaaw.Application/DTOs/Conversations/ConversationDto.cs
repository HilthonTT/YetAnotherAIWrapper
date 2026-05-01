namespace Yaaw.Application.DTOs.Conversations;

public sealed record ConversationDto
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required List<ConversationMessageDto> Messages { get; set; }
}
