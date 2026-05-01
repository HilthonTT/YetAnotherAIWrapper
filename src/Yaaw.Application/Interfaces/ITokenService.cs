namespace Yaaw.Application.Interfaces;

public interface ITokenService
{
    Task<string> GenerateTokenAsync(string identityUserId, string userId, string email, string name);
}
