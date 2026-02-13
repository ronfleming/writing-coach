using Shared.Models;

namespace Api.Services;

public interface ISessionRepository
{
    Task<SessionDocument> SaveAsync(SessionDocument session);
    Task<SessionDocument?> GetByIdAsync(string id, string userId);
    Task<List<SessionDocument>> GetByUserAsync(string userId, int limit = 20, string? continuationToken = null);
    Task<int> DeleteAllByUserAsync(string userId);
}

