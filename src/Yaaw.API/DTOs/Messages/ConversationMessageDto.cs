namespace Yaaw.API.DTOs.Messages;

public sealed record ConversationMessageDto
{
    public required Guid Id { get; set; }
    public required Guid ConversationId { get; set; }
    public required string Role { get; set; }
    public required string Text { get; set; }
}