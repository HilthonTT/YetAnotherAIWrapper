using Microsoft.EntityFrameworkCore;
using Yaaw.Domain.Entities;
using Yaaw.Domain.Interfaces;

namespace Yaaw.Infrastructure.Persistence.Repositories;

internal sealed class ConversationRepository(ApplicationDbContext dbContext) : IConversationRepository
{
    public async Task<List<Conversation>> GetAllByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await dbContext.Conversations
            .Where(c => c.UserId == userId)
            .Include(c => c.Messages)
            .ToListAsync(ct);
    }

    public Task<IQueryable<Conversation>> GetQueryableByUserIdAsync(string userId, CancellationToken ct = default)
    {
        IQueryable<Conversation> query = dbContext.Conversations
            .Where(c => c.UserId == userId);

        return Task.FromResult(query);
    }

    public async Task<Conversation?> GetByIdAsync(Guid id, string userId, CancellationToken ct = default)
    {
        return await dbContext.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
    }

    public async Task AddAsync(Conversation conversation, CancellationToken ct = default)
    {
        await dbContext.Conversations.AddAsync(conversation, ct);
    }

    public async Task<bool> ExistsAsync(Guid id, string userId, CancellationToken ct = default)
    {
        return await dbContext.Conversations
            .AnyAsync(c => c.Id == id && c.UserId == userId, ct);
    }

    public async Task<bool> UpdateNameAsync(Guid id, string userId, string newName, CancellationToken ct = default)
    {
        Conversation? conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (conversation is null)
        {
            return false;
        }

        conversation.Name = newName;
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, string userId, CancellationToken ct = default)
    {
        int deleted = await dbContext.Conversations
            .Where(c => c.Id == id && c.UserId == userId)
            .ExecuteDeleteAsync(ct);

        return deleted > 0;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await dbContext.SaveChangesAsync(ct);
    }
}
