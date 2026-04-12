namespace Yaaw.API.Entities;

public sealed class ConversationMessage
{
    public required Guid Id { get; set; }
    public required Guid ConversationId { get; set; }
    public required string Role { get; set; }
    public required string Text { get; set; }
}