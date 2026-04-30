using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace Yaaw.Web.Services;

public sealed class JwtDelegatingHandler(TokenStorageService tokenStorage) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            // TODO: Fix this later
            string token = await tokenStorage.GetTokenAsync() ??
                "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1XzAxOWRkZTFlLWIzMGUtNzhiMS1hNjI3LTYzNTIyODZlMTEzNiIsImVtYWlsIjoidGVzdHVzZXJAMTIzLmNvbSIsIm5hbWUiOiJBYmMtMTIzNCIsImp0aSI6IjAxOWRkZTM4LTIwODMtN2UzYi04NTBjLTI4OWM0YzQ0MmRkZCIsImlkZW50aXR5X2lkIjoiM2NlM2VhY2MtNjIwMS00OTllLTlkNzItNzAwMWU5MjczYmM2IiwiaHR0cDovL3NjaGVtYXMubWljcm9zb2Z0LmNvbS93cy8yMDA4LzA2L2lkZW50aXR5L2NsYWltcy9yb2xlIjoiVXNlciIsImV4cCI6MTc3NzU1MzMxMywiaXNzIjoieWFhdy1hcGkiLCJhdWQiOiJ5YWF3LXdlYiJ9.rSva_f8Ithl-WzoO6jvINk0pfZzwyBKRGlN9tUe8nXY";

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch (JSDisconnectedException) { }
        catch (InvalidOperationException) { }

        return await base.SendAsync(request, cancellationToken);
    }
}
