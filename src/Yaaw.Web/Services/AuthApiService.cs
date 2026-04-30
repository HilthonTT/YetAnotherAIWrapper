using Yaaw.Web.Models;

namespace Yaaw.Web.Services;

public sealed class AuthApiService(HttpClient httpClient)
{
    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var body = new { request.Email, request.Name, request.Password };

        var response = await httpClient.PostAsJsonAsync("/api/auth/register", body, ct);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var body = new { request.Email, request.Password };

        var response = await httpClient.PostAsJsonAsync("/api/auth/login", body, ct);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
    }
}
