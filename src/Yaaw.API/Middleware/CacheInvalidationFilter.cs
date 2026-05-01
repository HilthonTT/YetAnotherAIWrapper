using System.Security.Claims;
using Yaaw.Application.Interfaces;

namespace Yaaw.API.Middleware;

internal sealed class CacheInvalidationFilter(IRedisCacheService cacheService) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        object? result = await next(context);

        if (result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 })
        {
            string? userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(userId))
            {
                await cacheService.RemoveByPrefixAsync($"cache:{userId}:/api/chat");
            }
        }

        return result;
    }
}
