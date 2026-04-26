using Yaaw.Web.Models;

namespace Yaaw.Web.Services;

public sealed class ChatApiService(HttpClient httpClient)
{
    public async Task<List<ConversationDto>> GetConversationsAsync(CancellationToken ct = default)
    {
        var response = await httpClient.GetFromJsonAsync<CollectionResponse<ConversationDto>>(
            "/api/chat", ct);

        return response?.Items ?? [];
    }

    public async Task<ConversationDto?> GetConversationAsync(Guid id, CancellationToken ct = default)
    {
        return await httpClient.GetFromJsonAsync<ConversationDto>(
            $"/api/chat/{id}", ct);
    }

    public async Task<ConversationDto?> CreateConversationAsync(string name, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            "/api/chat", new NewConversationDto(name), ct);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ConversationDto>(ct);
    }

    public async Task SendPromptAsync(Guid conversationId, string text, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/chat/{conversationId}", new PromptDto(text), ct);

        response.EnsureSuccessStatusCode();
    }

    public async Task RenameConversationAsync(Guid conversationId, string name, CancellationToken ct = default)
    {
        var response = await httpClient.PatchAsJsonAsync(
            $"/api/chat/{conversationId}", new RenameConversationDto(name), ct);

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync(
            $"/api/chat/{conversationId}", ct);

        response.EnsureSuccessStatusCode();
    }

    public async Task CancelGenerationAsync(Guid messageId, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsync(
            $"/api/chat/{messageId}/cancel", null, ct);

        response.EnsureSuccessStatusCode();
    }
}
