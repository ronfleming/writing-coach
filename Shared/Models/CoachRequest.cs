namespace Shared.Models;

/// <summary>
/// Request to the writing coach API
/// </summary>
public record CoachRequest
{
    /// <summary>
    /// The text to be analyzed and improved
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Target register: colloquial, neutral, or formal
    /// </summary>
    public Register Register { get; init; } = Register.Neutral;

    /// <summary>
    /// The goal of the transformation
    /// </summary>
    public Goal Goal { get; init; } = Goal.Correct;

    /// <summary>
    /// The context/scenario for the writing
    /// </summary>
    public Context Context { get; init; } = Context.General;

    /// <summary>
    /// Target language proficiency level
    /// </summary>
    public ProficiencyLevel TargetLevel { get; init; } = ProficiencyLevel.C1;
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
    B1,
    B2,
    C1,
    C2
}

