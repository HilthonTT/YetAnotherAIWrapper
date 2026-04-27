namespace Yaaw.API.DTOs.Conversations;

public sealed record ConversationQueryParameters
{
    public string? Search { get; init; }
    public string? Sort { get; init; }
    public string? Fields { get; init; }
}
