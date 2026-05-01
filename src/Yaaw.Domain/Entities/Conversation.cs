namespace Yaaw.Domain.Entities;

public sealed class Conversation
{
    public required Guid Id { get; set; }

    public required string Name { get; set; }

    public required string UserId { get; set; }

    public User? User { get; set; }

    public required List<ConversationMessage> Messages { get; set; } = [];
}
