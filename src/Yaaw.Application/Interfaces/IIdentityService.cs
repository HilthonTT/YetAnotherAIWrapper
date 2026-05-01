namespace Yaaw.Application.Interfaces;

public interface IIdentityService
{
    Task<(bool Succeeded, string? IdentityUserId, IDictionary<string, string[]>? Errors)> CreateUserAsync(string email, string password);
    Task<(bool Succeeded, string? IdentityUserId)> ValidateCredentialsAsync(string email, string password);
    Task AddToRoleAsync(string identityUserId, string role);
    Task<IList<string>> GetRolesAsync(string identityUserId);
}
