using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Yaaw.Application.Interfaces;

namespace Yaaw.Infrastructure.Identity;

internal sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string GetUserId()
    {
        string? userId = httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)
            ?.Value;

        return userId ?? throw new UnauthorizedAccessException("User is not authenticated.");
    }
}
