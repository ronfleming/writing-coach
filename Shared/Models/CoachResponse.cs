namespace Shared.Models;

/// <summary>
/// Response from the writing coach API
/// </summary>
public record CoachResponse
{
    /// <summary>
    /// The original text that was submitted
    /// </summary>
    public required string OriginalText { get; init; }

    /// <summary>
    /// Minimal corrections (grammar, spelling, agreement, word order only)
    /// </summary>
    public required string MinimalFix { get; init; }

    /// <summary>
    /// Enhanced version with natural phrasing, idioms, and register-appropriate language
    /// </summary>
    public required string C1Upgrade { get; init; }

    /// <summary>
    /// Optional style variants in different registers
    /// </summary>
    public StyleVariants? Variants { get; init; }

    /// <summary>
    /// Top issues identified with explanations (max 5)
    /// </summary>
    public List<FeedbackItem> Feedback { get; init; } = [];

    /// <summary>
    /// Reusable phrases extracted from the improved text
    /// </summary>
    public List<PhraseEntry> PhraseBank { get; init; } = [];

    /// <summary>
    /// Tags for recurring error categories
    /// </summary>
    public List<string> ErrorTags { get; init; } = [];
}

public record StyleVariants
{
    public string? Colloquial { get; init; }
    public string? Neutral { get; init; }
    public string? Formal { get; init; }
}

public record FeedbackItem
{
    /// <summary>
    /// Brief description of the issue
    /// </summary>
    public required string Issue { get; init; }

    /// <summary>
    /// Why this matters for clear communication
    /// </summary>
    public required string WhyItMatters { get; init; }

    /// <summary>
    /// A memorable rule of thumb
    /// </summary>
    public required string QuickRule { get; init; }

    /// <summary>
    /// Concrete example showing the correction
    /// </summary>
    public required string Example { get; init; }

    /// <summary>
    /// Error category tag (e.g., "prep-case", "word-order")
    /// </summary>
    public string? Tag { get; init; }
}

public record PhraseEntry
{
    /// <summary>
    /// The German phrase
    /// </summary>
    public required string German { get; init; }

    /// <summary>
    /// English translation/meaning
    /// </summary>
    public required string English { get; init; }

    /// <summary>
    /// Grammatical case if applicable (Akkusativ, Dativ, etc.)
    /// </summary>
    public string? Case { get; init; }

    /// <summary>
    /// Additional notes about usage
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Tags for categorization (verb-prep, idiom, connector, etc.)
    /// </summary>
    public List<string> Tags { get; init; } = [];
}

