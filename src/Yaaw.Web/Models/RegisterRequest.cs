namespace Yaaw.Web.Models;

public sealed record RegisterRequest(string Email, string Name, string Password, string ConfirmPassword);
