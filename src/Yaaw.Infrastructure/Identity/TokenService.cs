using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Yaaw.Application.Interfaces;
using Yaaw.Infrastructure.Settings;

namespace Yaaw.Infrastructure.Identity;

internal sealed class TokenService(
    UserManager<IdentityUser> userManager,
    IOptions<JwtOptions> jwtOptions) : ITokenService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<string> GenerateTokenAsync(string identityUserId, string userId, string email, string name)
    {
        var identityUser = await userManager.FindByIdAsync(identityUserId);
        var roles = identityUser is not null
            ? await userManager.GetRolesAsync(identityUser)
            : (IList<string>)[];

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Name, name),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new("identity_id", identityUserId),
        ];

        foreach (string role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
