namespace Yaaw.API.DTOs.Auth;

public sealed record UserProfileDto(string Id, string Email, string Name, DateTime CreatedAtUtc);
