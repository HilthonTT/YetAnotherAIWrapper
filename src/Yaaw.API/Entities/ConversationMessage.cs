namespace Yaaw.API.Entities;

public sealed class ConversationMessage
{
    public required string Id { get; set; }
    public required string ConversationId { get; set; }
    public required string Role { get; set; }
    public required string Text { get; set; }

    public static string NewId() => $"cm_{Guid.CreateVersion7()}";
}