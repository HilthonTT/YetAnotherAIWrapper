namespace Yaaw.API.Services;

internal sealed class RedisConversationStateHostedService(RedisConversationState state) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => state.StartAsync();

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
