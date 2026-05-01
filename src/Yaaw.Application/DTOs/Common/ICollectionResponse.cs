namespace Yaaw.Application.DTOs.Common;

public interface ICollectionResponse<T>
{
    List<T> Items { get; init; }
}
