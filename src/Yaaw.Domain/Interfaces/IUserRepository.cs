using Yaaw.Domain.Entities;

namespace Yaaw.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdentityIdAsync(string identityId, CancellationToken ct = default);

    Task<User?> GetByIdAsync(string id, CancellationToken ct = default);

    Task AddAsync(User user, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
