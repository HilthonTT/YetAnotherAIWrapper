namespace Yaaw.API.DTOs.Conversations;

public sealed class ConversationMessageDto
{
    public required Guid Id { get; set; }
    public required Guid ConversationId { get; set; }
    public required string Role { get; set; }
    public required string Text { get; set; }
}