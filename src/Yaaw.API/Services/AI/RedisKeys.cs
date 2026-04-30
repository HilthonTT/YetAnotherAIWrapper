using StackExchange.Redis;

namespace Yaaw.API.Services.AI;

internal static class RedisKeys
{
    public static RedisKey GetBacklogKey(Guid conversationId) => 
        $"conversation:{conversationId}:backlog";

    public static RedisChannel GetRedisChannelName(Guid conversationId) => 
        RedisChannel.Literal($"conversation:{conversationId}:channel");
}
