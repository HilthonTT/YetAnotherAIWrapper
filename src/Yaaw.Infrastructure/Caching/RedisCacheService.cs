using System.Text.Json;
using StackExchange.Redis;
using Yaaw.Application.Interfaces;

namespace Yaaw.Infrastructure.Caching;

internal sealed class RedisCacheService(
    IConnectionMultiplexer redis,
    ICacheKeyManager cacheKeyManager) : IRedisCacheService
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromSeconds(CacheKey.DefaultExpirationSeconds);

    private const string DataField = "data";
    private const string ETagField = "etag";

    public async Task<(T? Value, string? ETag)> GetAsync<T>(string key, CancellationToken ct = default)
    {
        IDatabase db = redis.GetDatabase();

        HashEntry[] entries = await db.HashGetAllAsync(key);
        if (entries.Length == 0)
        {
            return (default, null);
        }

        string? data = null;
        string? etag = null;

        foreach (HashEntry entry in entries)
        {
            if (entry.Name == DataField)
            {
                data = entry.Value;
            }
            else if (entry.Name == ETagField)
            {
                etag = entry.Value;
            }
        }

        if (data is null)
        {
            return (default, null);
        }

        T? value = JsonSerializer.Deserialize<T>(data);
        return (value, etag);
    }

    public async Task SetAsync<T>(string key, T value, string etag, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        IDatabase db = redis.GetDatabase();
        string json = JsonSerializer.Serialize(value);

        HashEntry[] entries =
        [
            new(DataField, json),
            new(ETagField, etag),
        ];

        await db.HashSetAsync(key, entries);
        await db.KeyExpireAsync(key, expiration ?? DefaultExpiration);

        cacheKeyManager.AddKey(key);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        IDatabase db = redis.GetDatabase();
        await db.KeyDeleteAsync(key);
        cacheKeyManager.RemoveKey(key);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        IDatabase db = redis.GetDatabase();

        // Remove keys tracked in-memory
        IEnumerable<string> removedKeys = cacheKeyManager.RemoveByPrefix(prefix);
        foreach (string key in removedKeys)
        {
            await db.KeyDeleteAsync(key);
        }

        // Also scan Redis directly for matching keys in case the in-memory
        // tracker lost state (e.g. after an app restart).
        var server = redis.GetServers().FirstOrDefault();
        if (server is not null)
        {
            await foreach (var key in server.KeysAsync(pattern: $"{prefix}*"))
            {
                await db.KeyDeleteAsync(key);
            }
        }
    }
}
