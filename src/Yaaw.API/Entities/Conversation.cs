namespace Yaaw.API.Entities;

public sealed class Conversation
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required List<ConversationMessage> Messages { get; set; } = [];

    public static string NewId() => $"c_{Guid.CreateVersion7()}";
}
