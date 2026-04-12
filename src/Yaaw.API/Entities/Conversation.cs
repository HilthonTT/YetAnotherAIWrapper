namespace Yaaw.API.Entities;

public sealed class Conversation
{
    public required Guid Id { get; set; }

    public required string Name { get; set; }

    public required List<ConversationMessage> Messages { get; set; } = [];
}
