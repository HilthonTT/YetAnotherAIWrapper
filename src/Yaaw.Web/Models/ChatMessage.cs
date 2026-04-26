namespace Yaaw.Web.Models;

/// <summary>
/// Represents a message in the local UI state, which may be being streamed.
/// </summary>
public sealed class ChatMessage
{
    public Guid Id { get; set; }
    public string Role { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsStreaming { get; set; }
    public bool IsPlaceholder { get; set; }
}
