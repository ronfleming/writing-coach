namespace Shared.Models;

public record CoachRequest
{
    public required string Text { get; init; }
    public string TargetLanguage { get; init; } = "de";
    public Register Register { get; init; } = Register.Neutral;
    public Goal Goal { get; init; } = Goal.Correct;
    public Context Context { get; init; } = Context.General;
    public ProficiencyLevel TargetLevel { get; init; } = ProficiencyLevel.C1;
    public AIModel Model { get; init; } = AIModel.Gpt4oMini;
}

public enum Register
{
    Colloquial,
    Neutral,
    Formal
}

public enum Goal
{
    Correct,
    Shorten,
    Clarify,
    Persuade,
    Diplomatic
}

public enum Context
{
    General,
    Recruiter,
    CoverLetter,
    Behoerden,
    Landlord,
    Bank,
    Insurance
}

public enum ProficiencyLevel
{
    A1,
    A2,
    B1,
    B2,
    C1,
    C2
}

public enum AIModel
{
    Gpt4oMini,
    Gpt41Mini,
    Gpt4o,
    Gpt41
}

/// <summary>
/// The minimum access tier required to use a model.
/// Higher numeric value = more restrictive.
/// </summary>
public enum ModelAccessTier
{
    /// <summary>Available to everyone, including anonymous users.</summary>
    Free = 0,

    /// <summary>Requires a signed-in account.</summary>
    Authenticated = 1,

    /// <summary>Requires a premium plan (coming soon).</summary>
    Premium = 2
}

public static class AIModelInfo
{
    public record ModelDetails(
        string DisplayName,
        string ApiModelId,
        ModelAccessTier RequiredTier,
        string Description,
        string ApproxCostPerRequest
    )
    {
        /// <summary>Backwards-compatible convenience property.</summary>
        public bool IsPremium => RequiredTier >= ModelAccessTier.Premium;
    };

    private static readonly Dictionary<AIModel, ModelDetails> Models = new()
    {
        [AIModel.Gpt4oMini] = new("GPT-4o mini", "gpt-4o-mini",
            ModelAccessTier.Free, "Fast & cheap, decent quality", "~$0.001"),
        [AIModel.Gpt41Mini] = new("GPT-4.1 mini", "gpt-4.1-mini",
            ModelAccessTier.Authenticated, "Best value — strong instruction following", "~$0.002"),
        [AIModel.Gpt4o] = new("GPT-4o", "gpt-4o",
            ModelAccessTier.Premium, "High quality, better nuance", "~$0.01"),
        [AIModel.Gpt41] = new("GPT-4.1", "gpt-4.1",
            ModelAccessTier.Premium, "Top tier — best comprehension", "~$0.01"),
    };

    public static ModelDetails Get(AIModel model) => Models[model];

    public static IReadOnlyDictionary<AIModel, ModelDetails> All => Models;

    public static string GetApiModelId(AIModel model) => Models[model].ApiModelId;

    /// <summary>True if the given tier is sufficient to use the model.</summary>
    public static bool CanAccess(AIModel model, ModelAccessTier userTier) =>
        Models[model].RequiredTier <= userTier;

    /// <summary>Returns all models accessible at the given tier.</summary>
    public static IEnumerable<AIModel> GetAccessibleModels(ModelAccessTier userTier) =>
        Models.Where(m => m.Value.RequiredTier <= userTier).Select(m => m.Key);
}
