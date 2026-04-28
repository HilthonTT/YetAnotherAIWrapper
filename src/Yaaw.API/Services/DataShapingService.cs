using System.Collections.Concurrent;
using System.Dynamic;
using System.Reflection;
using Yaaw.API.DTOs.Common;

namespace Yaaw.API.Services;

internal sealed class DataShapingService
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesCache = new();

    public ExpandoObject ShapeData<T>(T entity, string? fields)
    {
        PropertyInfo[] properties = GetProperties<T>(fields);
        return ShapeEntity(entity, properties);
    }

    public List<ExpandoObject> ShapeCollectionData<T>(
        IEnumerable<T> entities,
        string? fields,
        Func<T, List<LinkDto>>? linkFactory = null)
    {
        PropertyInfo[] properties = GetProperties<T>(fields);

        List<ExpandoObject> shapedObjects = [];
        foreach (T entity in entities)
        {
            ExpandoObject shaped = ShapeEntity(entity, properties);

            if (linkFactory is not null)
            {
                ((IDictionary<string, object?>)shaped)["links"] = linkFactory(entity);
            }

            shapedObjects.Add(shaped);
        }

        return shapedObjects;
    }

    public bool Validate<T>(string? fields)
    {
        if (string.IsNullOrWhiteSpace(fields))
        {
            return true;
        }

        HashSet<string> requestedFields = ParseFields(fields);
        PropertyInfo[] allProperties = GetCachedProperties<T>();

        return requestedFields.All(f =>
            allProperties.Any(p => p.Name.Equals(f, StringComparison.OrdinalIgnoreCase)));
    }

    private static PropertyInfo[] GetProperties<T>(string? fields)
    {
        PropertyInfo[] allProperties = GetCachedProperties<T>();

        if (string.IsNullOrWhiteSpace(fields))
        {
            return allProperties;
        }

        HashSet<string> requestedFields = ParseFields(fields);

        return allProperties
            .Where(p => requestedFields.Contains(p.Name))
            .ToArray();
    }

    private static PropertyInfo[] GetCachedProperties<T>()
    {
        return PropertiesCache.GetOrAdd(
            typeof(T),
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
    }

    private static ExpandoObject ShapeEntity<T>(T entity, PropertyInfo[] properties)
    {
        IDictionary<string, object?> shapedObject = new ExpandoObject();

        foreach (PropertyInfo property in properties)
        {
            shapedObject[property.Name] = property.GetValue(entity);
        }

        return (ExpandoObject)shapedObject;
    }

    private static HashSet<string> ParseFields(string fields)
    {
        return fields
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
