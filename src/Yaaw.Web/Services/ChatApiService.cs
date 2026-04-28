using System.Web;
using Yaaw.Web.Models;

namespace Yaaw.Web.Services;

public sealed class ChatApiService(HttpClient httpClient)
{
    public async Task<List<ConversationDto>> GetConversationsAsync(
        string? search = null,
        string? sort = null,
        string? fields = null,
        CancellationToken ct = default)
    {
        string url = BuildUrl("/api/chat", [
            ("search", search),
            ("sort", sort),
            ("fields", fields),
        ]);

        var response = await httpClient.GetFromJsonAsync<CollectionResponse<ConversationDto>>(url, ct);

        return response?.Items ?? [];
    }

    public async Task<ConversationDto?> GetConversationAsync(
        Guid id,
        string? fields = null,
        CancellationToken ct = default)
    {
        string url = BuildUrl($"/api/chat/{id}", [("fields", fields)]);

        return await httpClient.GetFromJsonAsync<ConversationDto>(url, ct);
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

    public async Task<ConversationDto?> RenameConversationAsync(
        Guid conversationId,
        string name,
        CancellationToken ct = default)
    {
        var response = await httpClient.PatchAsJsonAsync(
            $"/api/chat/{conversationId}", new RenameConversationDto(name), ct);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ConversationDto>(ct);
    }

    public async Task DeleteConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync(
            $"/api/chat/{conversationId}", ct);

        response.EnsureSuccessStatusCode();
    }

    public async Task CancelGenerationAsync(Guid conversationId, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsync(
            $"/api/chat/{conversationId}/cancel", null, ct);

        response.EnsureSuccessStatusCode();
    }

    private static string BuildUrl(string path, (string key, string? value)[] queryParams)
    {
        var filtered = queryParams
            .Where(p => !string.IsNullOrWhiteSpace(p.value))
            .Select(p => $"{HttpUtility.UrlEncode(p.key)}={HttpUtility.UrlEncode(p.value)}")
            .ToList();

        return filtered.Count > 0
            ? $"{path}?{string.Join('&', filtered)}"
            : path;
    }
}
