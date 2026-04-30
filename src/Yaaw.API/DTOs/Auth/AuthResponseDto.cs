namespace Yaaw.API.DTOs.Auth;

public sealed record AuthResponseDto(string Token, string UserId, string Email, string Name);
