namespace Yaaw.Web.Models;

public sealed class ConversationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<ConversationMessageDto> Messages { get; set; } = [];
}
