namespace Yaaw.API.DTOs.Messages;

public sealed record ClientMessageDto(Guid Id, string Sender, string Text);
