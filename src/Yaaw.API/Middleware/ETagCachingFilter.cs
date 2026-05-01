using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Yaaw.Application.Interfaces;

namespace Yaaw.API.Middleware;

internal sealed class ETagCachingFilter(IRedisCacheService cacheService) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        HttpContext httpContext = context.HttpContext;
        string? userId = httpContext.User
            .FindFirst(ClaimTypes.NameIdentifier)
            ?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return await next(context);
        }

        string cacheKey = $"cache:{userId}:{httpContext.Request.Path}{httpContext.Request.QueryString}";
        string? ifNoneMatch = httpContext.Request.Headers.IfNoneMatch.ToString();

        var (cachedValue, cachedETag) = await cacheService.GetAsync<JsonElement>(cacheKey);

        if (cachedETag is not null)
        {
            if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch.Trim('"') == cachedETag.Trim('"'))
            {
                httpContext.Response.Headers.ETag = $"\"{cachedETag}\"";
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            httpContext.Response.Headers.ETag = $"\"{cachedETag}\"";
            return Results.Ok(cachedValue);
        }

        object? result = await next(context);

        if (result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 } &&
            result is IValueHttpResult valueResult && valueResult.Value is not null)
        {
            string json = JsonSerializer.Serialize(valueResult.Value);
            string etag = ComputeETag(json);

            var element = JsonSerializer.Deserialize<JsonElement>(json);
            await cacheService.SetAsync(cacheKey, element, etag);

            httpContext.Response.Headers.ETag = $"\"{etag}\"";
        }

        return result;
    }

    private static string ComputeETag(string content)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(hash)[..16];
    }
}
