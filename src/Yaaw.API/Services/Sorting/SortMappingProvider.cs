using System.Linq.Dynamic.Core;

namespace Yaaw.API.Services.Sorting;

public sealed class SortMappingProvider(IEnumerable<ISortMappingDefinition> sortMappingDefinitions)
{
    public SortMapping[] GetMappings<TSource, TDestination>()
    {
        SortMappingDefinition<TSource, TDestination>? definition = sortMappingDefinitions
            .OfType<SortMappingDefinition<TSource, TDestination>>()
            .FirstOrDefault();

        return definition?.Mappings
            ?? throw new InvalidOperationException(
                $"No sort mapping defined from '{typeof(TSource).Name}' to '{typeof(TDestination).Name}'.");
    }

    public bool ValidateMappings<TSource, TDestination>(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        SortMapping[] mappings = GetMappings<TSource, TDestination>();

        return sort
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(f => f.Split(' ')[0])
            .All(f => mappings.Any(m => m.SortField.Equals(f, StringComparison.OrdinalIgnoreCase)));
    }
}