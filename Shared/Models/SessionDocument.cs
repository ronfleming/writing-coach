using System.Text.Json.Serialization;

namespace Shared.Models;

/// <summary>
/// Cosmos DB document for a coaching session. Partition key: /userId
/// </summary>
public record SessionDocument
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    public string UserId { get; init; } = "anonymous";
    public string Type { get; init; } = "session";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public required CoachRequest Request { get; init; }
    public required CoachResponse Response { get; init; }

    // Denormalized for query filtering
    public string TargetLanguage { get; init; } = "de";
    public string TargetLevel { get; init; } = "C1";
    public string? ModelUsed { get; init; }
}
