namespace Yaaw.API.Hubs;

public sealed record StreamContext(Guid? LastMessageId, Guid? LastFragmentId);