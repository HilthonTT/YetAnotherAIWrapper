using Microsoft.EntityFrameworkCore;
using Yaaw.Domain.Entities;
using Yaaw.Domain.Interfaces;

namespace Yaaw.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(ApplicationDbContext dbContext) : IUserRepository
{
    public async Task<User?> GetByIdentityIdAsync(string identityId, CancellationToken ct = default)
    {
        return await dbContext.Users
            .FirstOrDefaultAsync(u => u.IdentityId == identityId, ct);
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await dbContext.Users.AddAsync(user, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await dbContext.SaveChangesAsync(ct);
    }
}
