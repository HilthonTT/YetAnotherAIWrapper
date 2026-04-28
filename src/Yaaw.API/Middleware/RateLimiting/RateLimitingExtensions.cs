using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;

namespace Yaaw.API.Middleware.RateLimiting;

internal static class RateLimitingExtensions
{
    internal static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimiterOptions>(configuration.GetSection(RateLimiterOptions.SectionName));
        services.AddSingleton<SlidingWindowRateLimiter>();
        services.AddProblemDetails();

        return services;
    }

    internal static IApplicationBuilder UseRedisRateLimiting<TLimiter>(this IApplicationBuilder app)
        where TLimiter : class, IRateLimiter
    {
        return app.Use(async (context, next) =>
        {
            var limiter = context.RequestServices.GetRequiredService<TLimiter>();
            var rateLimiterOptions = context.RequestServices.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

            string clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (!await limiter.IsAllowedAsync(clientKey))
            {
                int retryAfterSeconds = (int)rateLimiterOptions.Window.TotalSeconds;

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = retryAfterSeconds.ToString();

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = $"Too many requests. Please try again after {retryAfterSeconds} seconds.",
                    Type = "https://tools.ietf.org/html/rfc6585#section-4",
                };

                await context.Response.WriteAsJsonAsync(problemDetails);
                return;
            }

            await next();
        });
    }
}