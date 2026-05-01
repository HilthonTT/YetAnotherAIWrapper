using Microsoft.AspNetCore.Identity;
using Yaaw.Application.Interfaces;

namespace Yaaw.Infrastructure.Identity;

internal sealed class IdentityService(UserManager<IdentityUser> userManager) : IIdentityService
{
    public async Task<(bool Succeeded, string? IdentityUserId, IDictionary<string, string[]>? Errors)> CreateUserAsync(string email, string password)
    {
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return (false, null, new Dictionary<string, string[]>
            {
                ["Email"] = ["A user with this email already exists."]
            });
        }

        var identityUser = new IdentityUser
        {
            UserName = email,
            Email = email,
        };

        IdentityResult result = await userManager.CreateAsync(identityUser, password);

        if (!result.Succeeded)
        {
            var errors = result.Errors.ToDictionary(
                e => e.Code,
                e => new[] { e.Description });

            return (false, null, errors);
        }

        return (true, identityUser.Id, null);
    }

    public async Task<(bool Succeeded, string? IdentityUserId)> ValidateCredentialsAsync(string email, string password)
    {
        var identityUser = await userManager.FindByEmailAsync(email);

        if (identityUser is null || !await userManager.CheckPasswordAsync(identityUser, password))
        {
            return (false, null);
        }

        return (true, identityUser.Id);
    }

    public async Task AddToRoleAsync(string identityUserId, string role)
    {
        var user = await userManager.FindByIdAsync(identityUserId);
        if (user is not null)
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }

    public async Task<IList<string>> GetRolesAsync(string identityUserId)
    {
        var user = await userManager.FindByIdAsync(identityUserId);
        if (user is null)
        {
            return [];
        }

        return await userManager.GetRolesAsync(user);
    }
}
