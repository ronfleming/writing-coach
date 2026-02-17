using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Api.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddSingleton<ICoachService, OpenAICoachService>();
builder.Services.AddSingleton<CosmosDbService>();
builder.Services.AddSingleton<ISessionRepository, CosmosSessionRepository>();
builder.Services.AddSingleton<IPhraseRepository, CosmosPhraseRepository>();
builder.Services.AddSingleton<RateLimitService>();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

var app = builder.Build();

var cosmosDb = app.Services.GetRequiredService<CosmosDbService>();
try
{
    await cosmosDb.InitializeAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogWarning(ex, "Cosmos DB initialization failed — persistence will be unavailable");
}

app.Run();
