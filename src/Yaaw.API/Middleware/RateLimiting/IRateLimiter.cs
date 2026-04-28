namespace Yaaw.API.Middleware.RateLimiting;

public interface IRateLimiter
{
    Task<bool> IsAllowedAsync(string key);
}
