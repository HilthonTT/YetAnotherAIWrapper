using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Yaaw.API.Services.Auth;

internal sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor)
{
    public string GetUserId()
    {
        string? userId = httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)
            ?.Value;

        return userId ?? throw new UnauthorizedAccessException("User is not authenticated.");
    }
}
