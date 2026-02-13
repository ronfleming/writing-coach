namespace Shared.Models;

public record CoachResponse
{
    public required string OriginalText { get; init; }
    public required string MinimalFix { get; init; }
    public required string UpgradedText { get; init; }
    public StyleVariants? Variants { get; init; }
    public List<FeedbackItem> Feedback { get; init; } = [];
    public List<PhraseEntry> PhraseBank { get; init; } = [];
    public List<string> ErrorTags { get; init; } = [];
    public string? RegisterNote { get; init; }
    public AlternativeInterpretation? Alternative { get; init; }
    public string? ModelUsed { get; init; }
    public TokenUsage? Usage { get; init; }
}

public record TokenUsage
{
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens => InputTokens + OutputTokens;
}

public record AlternativeInterpretation
{
    public required string OriginalPhrase { get; init; }
    public required string PrimaryMeaning { get; init; }
    public required string AlternativeMeaning { get; init; }
    public required string AlternativeText { get; init; }
}

public record StyleVariants
{
    public string? Colloquial { get; init; }
    public string? Neutral { get; init; }
    public string? Formal { get; init; }
}

public record FeedbackItem
{
    public required string Issue { get; init; }
    public required string WhyItMatters { get; init; }
    public required string QuickRule { get; init; }
    public required string Example { get; init; }
    public string? Tag { get; init; }
}

public record PhraseEntry
{
    public required string Phrase { get; init; }
    public string? Pattern { get; init; }
    public required string Translation { get; init; }
    public string? Level { get; init; }
    public string? GrammaticalInfo { get; init; }
    public string? Notes { get; init; }
    public List<string> Tags { get; init; } = [];
}
