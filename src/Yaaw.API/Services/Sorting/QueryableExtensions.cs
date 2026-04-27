using System.Linq.Dynamic.Core;

namespace Yaaw.API.Services.Sorting;

internal static class QueryableExtensions
{
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        string? sort,
        SortMapping[] mappings,
        string defaultOrderBy = "Id")
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return query.OrderBy(defaultOrderBy);
        }

        string[] sortFields = sort
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        var orderByParts = new List<string>(sortFields.Length);

        foreach (string field in sortFields)
        {
            (string sortField, bool isDescending) = ParseSortField(field);

            SortMapping? mapping = mappings.FirstOrDefault(m =>
                m.SortField.Equals(sortField, StringComparison.OrdinalIgnoreCase));

            if (mapping is null)
            {
                throw new InvalidOperationException(
                    $"Sort field '{sortField}' has no corresponding mapping.");
            }

            string direction = isDescending ^ mapping.Reverse ? "DESC" : "ASC";
            orderByParts.Add($"{mapping.PropertyName} {direction}");
        }

        return query.OrderBy(string.Join(", ", orderByParts));
    }

    private static (string SortField, bool IsDescending) ParseSortField(string field)
    {
        int spaceIndex = field.IndexOf(' ');

        if (spaceIndex < 0)
        {
            return (field, false);
        }

        string sortField = field[..spaceIndex];
        string directionToken = field[(spaceIndex + 1)..].TrimStart();
        bool isDescending = directionToken.Equals("desc", StringComparison.OrdinalIgnoreCase);

        return (sortField, isDescending);
    }
}
