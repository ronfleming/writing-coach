using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Services;

/// <summary>
/// Manages the Cosmos DB client and container initialization.
/// </summary>
public class CosmosDbService
{
    private readonly CosmosClient _client;
    private readonly ILogger<CosmosDbService> _logger;
    private Database? _database;
    private bool _initialized;

    public const string DatabaseName = "WritingCoach";
    public const string SessionsContainer = "sessions";
    public const string PhrasesContainer = "phrases";

    public CosmosDbService(IConfiguration configuration, ILogger<CosmosDbService> logger)
    {
        _logger = logger;

        var connectionString = configuration["CosmosDb:ConnectionString"]
            ?? throw new InvalidOperationException(
                "CosmosDb:ConnectionString is required. Set CosmosDb__ConnectionString in local.settings.json.");

        _client = new CosmosClient(connectionString, new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            },
            ConnectionMode = ConnectionMode.Gateway
        });
    }

    /// <summary>
    /// Ensures the database and containers exist. Called once at startup.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _logger.LogInformation("Initializing Cosmos DB: database={Database}", DatabaseName);

        var dbResponse = await _client.CreateDatabaseIfNotExistsAsync(DatabaseName);
        _database = dbResponse.Database;

        await _database.CreateContainerIfNotExistsAsync(new ContainerProperties
        {
            Id = SessionsContainer,
            PartitionKeyPath = "/userId",
            DefaultTimeToLive = -1
        });

        await _database.CreateContainerIfNotExistsAsync(new ContainerProperties
        {
            Id = PhrasesContainer,
            PartitionKeyPath = "/userId",
            DefaultTimeToLive = -1
        });

        _initialized = true;
        _logger.LogInformation("Cosmos DB initialized successfully");
    }

    public Container GetSessionsContainer()
    {
        EnsureInitialized();
        return _client.GetContainer(DatabaseName, SessionsContainer);
    }

    public Container GetPhrasesContainer()
    {
        EnsureInitialized();
        return _client.GetContainer(DatabaseName, PhrasesContainer);
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("CosmosDbService has not been initialized. Call InitializeAsync first.");
    }
}

