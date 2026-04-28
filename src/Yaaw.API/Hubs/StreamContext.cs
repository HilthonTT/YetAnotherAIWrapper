namespace Yaaw.API.Hubs;

internal sealed record StreamContext(Guid? LastMessageId, Guid? LastFragmentId);
