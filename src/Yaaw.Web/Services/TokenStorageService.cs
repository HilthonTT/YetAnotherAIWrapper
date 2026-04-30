using Microsoft.JSInterop;

namespace Yaaw.Web.Services;

public sealed class TokenStorageService(IJSRuntime jsRuntime)
{
    private const string TokenKey = "auth_token";
    private const string UserIdKey = "auth_user_id";
    private const string UserEmailKey = "auth_user_email";
    private const string UserNameKey = "auth_user_name";

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<string?>("authStorage.get", TokenKey);
        }
        catch (JSDisconnectedException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    public async Task SetTokenAsync(string token)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("authStorage.set", TokenKey, token);
        }
        catch (JSDisconnectedException) { }
        catch (InvalidOperationException) { }
    }

    public async Task<(string? UserId, string? Email, string? Name)> GetUserAsync()
    {
        try
        {
            var userId = await jsRuntime.InvokeAsync<string?>("authStorage.get", UserIdKey);
            var email = await jsRuntime.InvokeAsync<string?>("authStorage.get", UserEmailKey);
            var name = await jsRuntime.InvokeAsync<string?>("authStorage.get", UserNameKey);

            return (userId, email, name);
        }
        catch (JSDisconnectedException) { return (null, null, null); }
        catch (InvalidOperationException) { return (null, null, null); }
    }

    public async Task SetUserAsync(string userId, string email, string name)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("authStorage.set", UserIdKey, userId);
            await jsRuntime.InvokeVoidAsync("authStorage.set", UserEmailKey, email);
            await jsRuntime.InvokeVoidAsync("authStorage.set", UserNameKey, name);
        }
        catch (JSDisconnectedException) { }
        catch (InvalidOperationException) { }
    }

    public async Task ClearAsync()
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("authStorage.remove", TokenKey);
            await jsRuntime.InvokeVoidAsync("authStorage.remove", UserIdKey);
            await jsRuntime.InvokeVoidAsync("authStorage.remove", UserEmailKey);
            await jsRuntime.InvokeVoidAsync("authStorage.remove", UserNameKey);
        }
        catch (JSDisconnectedException) { }
        catch (InvalidOperationException) { }
    }
}
