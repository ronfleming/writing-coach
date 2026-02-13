using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace Api.Services;

public class CosmosSessionRepository : ISessionRepository
{
    private readonly CosmosDbService _cosmosDb;
    private readonly ILogger<CosmosSessionRepository> _logger;

    public CosmosSessionRepository(CosmosDbService cosmosDb, ILogger<CosmosSessionRepository> logger)
    {
        _cosmosDb = cosmosDb;
        _logger = logger;
    }

    public async Task<SessionDocument> SaveAsync(SessionDocument session)
    {
        var container = _cosmosDb.GetSessionsContainer();
        var response = await container.CreateItemAsync(session, new PartitionKey(session.UserId));

        _logger.LogInformation("Saved session {SessionId} for user {UserId} ({RU} RUs)",
            session.Id, session.UserId, response.RequestCharge);

        return response.Resource;
    }

    public async Task<SessionDocument?> GetByIdAsync(string id, string userId)
    {
        var container = _cosmosDb.GetSessionsContainer();

        try
        {
            var response = await container.ReadItemAsync<SessionDocument>(id, new PartitionKey(userId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<SessionDocument>> GetByUserAsync(string userId, int limit = 20, string? continuationToken = null)
    {
        var container = _cosmosDb.GetSessionsContainer();

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.userId = @userId AND c.type = 'session' ORDER BY c.createdAt DESC")
            .WithParameter("@userId", userId);

        var options = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(userId),
            MaxItemCount = limit
        };

        var results = new List<SessionDocument>();

        using var iterator = container.GetItemQueryIterator<SessionDocument>(query, continuationToken, options);

        if (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            results.AddRange(page);

            _logger.LogInformation("Fetched {Count} sessions for user {UserId} ({RU} RUs)",
                results.Count, userId, page.RequestCharge);
        }

        return results;
    }

    public async Task<int> DeleteAllByUserAsync(string userId)
    {
        var container = _cosmosDb.GetSessionsContainer();

        var query = new QueryDefinition("SELECT c.id FROM c WHERE c.userId = @userId AND c.type = 'session'")
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
                await container.DeleteItemAsync<SessionDocument>(id, new PartitionKey(userId));
                deleted++;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
            }
        }

        _logger.LogInformation("Deleted {Count} sessions for user {UserId}", deleted, userId);
        return deleted;
    }
}

