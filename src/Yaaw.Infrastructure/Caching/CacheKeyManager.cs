using System.Collections.Concurrent;
using Yaaw.Application.Interfaces;

namespace Yaaw.Infrastructure.Caching;

internal sealed class CacheKeyManager : ICacheKeyManager
{
    private readonly ConcurrentDictionary<string, byte> _keys = new();

    public IEnumerable<string> Keys => _keys.Keys;

    public void AddKey(string key)
    {
        _keys.TryAdd(key, 0);
    }

    public void Clear()
    {
        _keys.Clear();
    }

    public IEnumerable<string> RemoveByPrefix(string prefix)
    {
        List<string> removed = [];

        foreach (string key in _keys.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal) && _keys.TryRemove(key, out _))
            {
                removed.Add(key);
            }
        }

        return removed;
    }

    public void RemoveKey(string key)
    {
        _keys.TryRemove(key, out _);
    }
}
