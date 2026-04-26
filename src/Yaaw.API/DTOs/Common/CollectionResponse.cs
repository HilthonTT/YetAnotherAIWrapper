namespace Yaaw.API.DTOs.Common;

public class CollectionResponse<T> : ICollectionResponse<T>
{
    public List<T> Items { get; init; } = [];
}
