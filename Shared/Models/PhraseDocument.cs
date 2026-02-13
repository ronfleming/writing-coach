using System.Text.Json.Serialization;

namespace Shared.Models;

/// <summary>
/// Cosmos DB document for a phrase bank entry. Partition key: /userId
/// </summary>
public record PhraseDocument
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    public string UserId { get; init; } = "anonymous";
    public string Type { get; init; } = "phrase";
    public string? SourceSessionId { get; init; }

    public required string Phrase { get; init; }
    public string? Pattern { get; init; }
    public required string Translation { get; init; }
    public string? GrammaticalInfo { get; init; }
    public string? Notes { get; init; }
    public List<string> Tags { get; init; } = [];

    public string TargetLanguage { get; init; } = "de";
    public string TargetLevel { get; init; } = "C1";

    /// <summary>
    /// Intrinsic CEFR level of the phrase itself (A1–C2), independent of the user's target level.
    /// </summary>
    public string? PhraseLevel { get; init; }

    public string Status { get; init; } = "learning";
    public bool IsFavorite { get; init; } = false;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LearnedAt { get; init; }
}
