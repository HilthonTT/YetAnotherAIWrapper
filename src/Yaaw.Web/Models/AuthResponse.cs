namespace Yaaw.Web.Models;

public sealed record AuthResponse(string Token, string UserId, string Email, string Name);
