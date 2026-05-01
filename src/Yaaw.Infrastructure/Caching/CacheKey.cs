namespace Yaaw.Infrastructure.Caching;

public partial class CacheKey
{
    public const int DefaultExpirationSeconds = 60;

    public string Key { get; protected set; }

    public CacheKey(string key)
    {
        Key = key;
    }

    public virtual CacheKey Create(Func<object, object> createCacheKeyParameters, params object[] keyObjects)
    {
        var cacheKey = new CacheKey(Key);

        if (keyObjects.Length == 0)
        {
            return cacheKey;
        }

        cacheKey.Key = string.Format(Key, keyObjects.Select(k => createCacheKeyParameters(k)).ToArray());

        return cacheKey;
    }
}
