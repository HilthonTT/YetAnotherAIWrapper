using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Yaaw.API.Middleware.RateLimiting;

public sealed class FixedWindowRateLimiter(
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<RateLimiterOptions> options,
    ILogger<FixedWindowRateLimiter> logger) : IRateLimiter
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase()
        ?? throw new InvalidOperationException("Failed to get Redis database instance.");

    private readonly int _limit = options.Value.Limit;
    private readonly TimeSpan _window = options.Value.Window;

    public async Task<bool> IsAllowedAsync(string key)
    {
        string redisKey = $"ratelimit:fixed:{key}";

        long count = await _database.StringIncrementAsync(redisKey);

        if (count == 1)
        {
            await _database.KeyExpireAsync(redisKey, _window);
        }

        if (count > _limit)
        {
            logger.LogWarning("Fixed window rate limit exceeded for key: {Key}. Count: {Count}, Limit: {Limit}",
                key, count, _limit);
            return false;
        }

        logger.LogInformation(
            "Request allowed for key: {Key}. Count: {Count}, Limit: {Limit}", 
            key, count, _limit);

        return true;
    }
}
