using Shared.Models;

namespace Api.Services;

public interface IPhraseRepository
{
    Task SaveBatchAsync(List<PhraseDocument> phrases);
    Task<List<PhraseDocument>> GetByUserAsync(string userId, string? phraseLevel = null, string? status = null, int limit = 50);
    Task<PhraseDocument?> UpdateStatusAsync(string id, string userId, string newStatus);
    Task<PhraseDocument?> ToggleFavoriteAsync(string id, string userId);
    Task<int> DeleteAllByUserAsync(string userId);
}

