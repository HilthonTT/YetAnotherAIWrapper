using Yaaw.Domain.Entities;

namespace Yaaw.Domain.Interfaces;

public interface IConversationRepository
{
    Task<List<Conversation>> GetAllByUserIdAsync(string userId, CancellationToken ct = default);

    Task<IQueryable<Conversation>> GetQueryableByUserIdAsync(string userId, CancellationToken ct = default);

    Task<Conversation?> GetByIdAsync(Guid id, string userId, CancellationToken ct = default);

    Task AddAsync(Conversation conversation, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid id, string userId, CancellationToken ct = default);

    Task<bool> UpdateNameAsync(Guid id, string userId, string newName, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid id, string userId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
