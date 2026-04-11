namespace Yaaw.AppHost.Extensions;

public sealed class AIModel(string name) : Resource(name), IResourceWithConnectionString, IResourceWithoutLifetime
{
    // For tracking
    internal IResource? UnderlyingResource { get; set; }
    internal ReferenceExpression? ConnectionString { get; set; }

    public ReferenceExpression ConnectionStringExpression =>
        ConnectionString ?? throw new InvalidOperationException("No connection string available.");
}