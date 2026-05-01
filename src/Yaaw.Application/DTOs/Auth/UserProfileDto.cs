namespace Yaaw.Application.DTOs.Auth;

public sealed record UserProfileDto(string Id, string Email, string Name, DateTime CreatedAtUtc);
