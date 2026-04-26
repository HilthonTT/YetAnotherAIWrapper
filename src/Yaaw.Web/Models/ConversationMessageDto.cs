namespace Yaaw.Web.Models;

public sealed class ConversationMessageDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = "";
    public string Text { get; set; } = "";
}
