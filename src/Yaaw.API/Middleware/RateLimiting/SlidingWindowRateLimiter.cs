using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Yaaw.API.Middleware.RateLimiting;

public sealed class SlidingWindowRateLimiter(
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<SlidingWindowRateLimiter> logger,
    IOptions<RateLimiterOptions> options) : IRateLimiter
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase()
        ?? throw new InvalidOperationException("Failed to get Redis database instance.");

    private readonly int _limit = options.Value.Limit;
    private readonly TimeSpan _window = options.Value.Window;

    public async Task<bool> IsAllowedAsync(string key)
    {
        string redisKey = $"rate_limit:sliding:{key}";
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long windowStart = now - (long)_window.TotalMilliseconds;

        // Step 1: Remove timestamps that fall outside the sliding window.
        await _database.SortedSetRemoveRangeByScoreAsync(redisKey, 0, windowStart);

        // Step 2: Get the current count of requests in the sliding window.
        var currentCount = await _database.SortedSetLengthAsync(redisKey);

        if (currentCount >= _limit)
        {
            logger.LogWarning("Rate limit exceeded for key: {Key}. Current count: {Count}, Limit: {Limit}",
                key, currentCount, _limit);
            return false; // Rate limit exceeded
        }

        // Step 3: Add the current request timestamp to the sorted set. (Use the timestamp as both the score and the value for uniqueness.) 
        string requestId = $"{now}-{Guid.CreateVersion7()}";
        await _database.SortedSetAddAsync(redisKey, requestId, now);

        // Step 4: Set an expiration on the key to prevent stale data.
        await _database.KeyExpireAsync(redisKey, _window);

        logger.LogInformation("Request allowed for key: {Key}. Current count: {Count}, Limit: {Limit}",
            key, currentCount + 1, _limit);

        return true;
    }
}
