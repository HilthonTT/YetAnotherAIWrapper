namespace Yaaw.Web.Models;

public sealed record CollectionResponse<T>
{
    public List<T> Items { get; set; } = [];
}
