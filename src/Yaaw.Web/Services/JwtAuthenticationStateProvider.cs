using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Yaaw.Web.Models;

namespace Yaaw.Web.Services;

public sealed class JwtAuthenticationStateProvider(TokenStorageService tokenStorage) : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            string? token = await tokenStorage.GetTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                return AnonymousState;
            }

            var claims = ParseClaimsFromJwt(token);

            if (claims is null || claims.Count == 0)
            {
                return AnonymousState;
            }

            var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
            if (expClaim is not null && long.TryParse(expClaim.Value, out long exp))
            {
                var expDate = DateTimeOffset.FromUnixTimeSeconds(exp);
                if (expDate <= DateTimeOffset.UtcNow)
                {
                    await tokenStorage.ClearAsync();
                    return AnonymousState;
                }
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            var principal = new ClaimsPrincipal(identity);

            return new AuthenticationState(principal);
        }
        catch (JSDisconnectedException)
        {
            return AnonymousState;
        }
        catch (InvalidOperationException)
        {
            return AnonymousState;
        }
    }

    public async Task LoginAsync(AuthResponse response)
    {
        await tokenStorage.SetTokenAsync(response.Token);
        await tokenStorage.SetUserAsync(response.UserId, response.Email, response.Name);

        var claims = ParseClaimsFromJwt(response.Token);
        var identity = new ClaimsIdentity(claims ?? [], "jwt");
        var principal = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
    }

    public async Task LogoutAsync()
    {
        await tokenStorage.ClearAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(AnonymousState));
    }

    private static List<Claim>? ParseClaimsFromJwt(string token)
    {
        try
        {
            string[] parts = token.Split('.');
            if (parts.Length != 3)
            {
                return null;
            }

            string payload = parts[1];

            // Fix base64 padding
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            byte[] jsonBytes = Convert.FromBase64String(payload);
            using var document = JsonDocument.Parse(jsonBytes);

            var claims = new List<Claim>();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        claims.Add(new Claim(property.Name, item.GetString() ?? ""));
                    }
                }
                else
                {
                    claims.Add(new Claim(property.Name, property.Value.ToString()));
                }
            }

            // Map standard JWT claims to ClaimsIdentity name/role
            var nameClaim = claims.FirstOrDefault(c => c.Type == "name");
            if (nameClaim is not null)
            {
                claims.Add(new Claim(ClaimTypes.Name, nameClaim.Value));
            }

            return claims;
        }
        catch
        {
            return null;
        }
    }
}
