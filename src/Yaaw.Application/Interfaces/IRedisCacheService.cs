namespace Yaaw.Application.Interfaces;

public interface IRedisCacheService
{
    Task<(T? Value, string? ETag)> GetAsync<T>(string key, CancellationToken ct = default);

    Task SetAsync<T>(string key, T value, string etag, TimeSpan? expiration = null, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);

    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
