using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace Api.Services;

public class CosmosPhraseRepository : IPhraseRepository
{
    private readonly CosmosDbService _cosmosDb;
    private readonly ILogger<CosmosPhraseRepository> _logger;

    public CosmosPhraseRepository(CosmosDbService cosmosDb, ILogger<CosmosPhraseRepository> logger)
    {
        _cosmosDb = cosmosDb;
        _logger = logger;
    }

    public async Task SaveBatchAsync(List<PhraseDocument> phrases)
    {
        if (phrases.Count == 0) return;

        var container = _cosmosDb.GetPhrasesContainer();
        var userId = phrases[0].UserId;
        double totalRu = 0;

        var batch = container.CreateTransactionalBatch(new PartitionKey(userId));
        foreach (var phrase in phrases)
        {
            batch.CreateItem(phrase);
        }

        var batchResponse = await batch.ExecuteAsync();
        totalRu = batchResponse.RequestCharge;

        if (!batchResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Phrase batch save partially failed for user {UserId}: {Status}",
                userId, batchResponse.StatusCode);

            totalRu = 0;
            foreach (var phrase in phrases)
            {
                try
                {
                    var response = await container.CreateItemAsync(phrase, new PartitionKey(userId));
                    totalRu += response.RequestCharge;
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    _logger.LogDebug("Phrase {PhraseId} already exists, skipping", phrase.Id);
                }
            }
        }

        _logger.LogInformation("Saved {Count} phrases for user {UserId} ({RU} RUs)",
            phrases.Count, userId, totalRu);
    }

    public async Task<List<PhraseDocument>> GetByUserAsync(
        string userId, string? phraseLevel = null, string? status = null, int limit = 50)
    {
        var container = _cosmosDb.GetPhrasesContainer();

        var queryParts = new List<string> { "c.userId = @userId", "c.type = 'phrase'" };

        if (!string.IsNullOrEmpty(phraseLevel))
            queryParts.Add("c.phraseLevel = @phraseLevel");

        if (!string.IsNullOrEmpty(status))
            queryParts.Add("c.status = @status");

        var sql = $"SELECT * FROM c WHERE {string.Join(" AND ", queryParts)} ORDER BY c.createdAt DESC";
        var queryDef = new QueryDefinition(sql)
            .WithParameter("@userId", userId);

        if (!string.IsNullOrEmpty(phraseLevel))
            queryDef = queryDef.WithParameter("@phraseLevel", phraseLevel);

        if (!string.IsNullOrEmpty(status))
            queryDef = queryDef.WithParameter("@status", status);

        var options = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(userId),
            MaxItemCount = limit
        };

        var results = new List<PhraseDocument>();

        using var iterator = container.GetItemQueryIterator<PhraseDocument>(queryDef, requestOptions: options);

        while (iterator.HasMoreResults && results.Count < limit)
        {
            var page = await iterator.ReadNextAsync();
            results.AddRange(page);
        }

        _logger.LogInformation("Fetched {Count} phrases for user {UserId} (level={Level}, status={Status})",
            results.Count, userId, phraseLevel ?? "all", status ?? "all");

        return results.Take(limit).ToList();
    }

    public async Task<PhraseDocument?> UpdateStatusAsync(string id, string userId, string newStatus)
    {
        var container = _cosmosDb.GetPhrasesContainer();

        try
        {
            var response = await container.ReadItemAsync<PhraseDocument>(id, new PartitionKey(userId));
            var existing = response.Resource;

            var updated = existing with
            {
                Status = newStatus,
                LearnedAt = newStatus == "learned" ? DateTimeOffset.UtcNow : null
            };

            var replaceResponse = await container.ReplaceItemAsync(updated, id, new PartitionKey(userId));

            _logger.LogInformation("Updated phrase {PhraseId} to status '{Status}' ({RU} RUs)",
                id, newStatus, replaceResponse.RequestCharge);

            return replaceResponse.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Phrase {PhraseId} not found for user {UserId}", id, userId);
            return null;
        }
    }

    public async Task<PhraseDocument?> ToggleFavoriteAsync(string id, string userId)
    {
        var container = _cosmosDb.GetPhrasesContainer();

        try
        {
            var response = await container.ReadItemAsync<PhraseDocument>(id, new PartitionKey(userId));
            var existing = response.Resource;

            var updated = existing with { IsFavorite = !existing.IsFavorite };

            var replaceResponse = await container.ReplaceItemAsync(updated, id, new PartitionKey(userId));

            _logger.LogInformation("Toggled favorite on phrase {PhraseId} to {IsFavorite} ({RU} RUs)",
                id, updated.IsFavorite, replaceResponse.RequestCharge);

            return replaceResponse.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Phrase {PhraseId} not found for user {UserId}", id, userId);
            return null;
        }
    }

    public async Task<int> DeleteAllByUserAsync(string userId)
    {
        var container = _cosmosDb.GetPhrasesContainer();

        var query = new QueryDefinition("SELECT c.id FROM c WHERE c.userId = @userId AND c.type = 'phrase'")
            .WithParameter("@userId", userId);

        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(userId) };

        var ids = new List<string>();
        using var iterator = container.GetItemQueryIterator<dynamic>(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            foreach (var item in page)
            {
                ids.Add((string)item.id);
            }
        }

        int deleted = 0;
        foreach (var id in ids)
        {
            try
            {
                await container.DeleteItemAsync<PhraseDocument>(id, new PartitionKey(userId));
                deleted++;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        _logger.LogInformation("Deleted {Count} phrases for user {UserId}", deleted, userId);
        return deleted;
    }
}

