using Client.Models;

namespace Client.Data;

public static class NewsData
{
    public static IReadOnlyList<NewsEntryMeta> Entries { get; } = new List<NewsEntryMeta>
    {
        new(
            Slug: "feedback-you-can-trust",
            Title: "Feedback You Can Trust: Filtering Out False-Positive Corrections",
            Summary: "A real session surfaced a 'fix' that wasn't a fix — the before and after were identical. Three new filters now drop hallucinated grammar feedback so every correction you see is one you can act on.",
            MetaDescription: "German Writing Coach now filters false-positive grammar feedback: identical before/after corrections are dropped, and hard-grammar feedback no longer contradicts Clean Entries.",
            PublishedDate: new DateOnly(2026, 5, 1),
            ContentComponent: typeof(Client.Components.NewsEntries.NewsEntry_TrustworthyFeedback),
            Keywords: "German writing feedback accuracy, AI grammar checker false positives, trustworthy German writing tool, German subordinate clause verb position, Goethe-Institut writing practice, AI hallucination filter"
        ),
        new(
            Slug: "writing-prompts-and-clean-entry-tracking",
            Title: "New Features: Writing Prompts for Every Level and Clean Entry Tracking",
            Summary: "Over 200 writing prompts matched to your CEFR level and context, plus Clean Entry tracking to highlight when your German writing needs no corrections.",
            MetaDescription: "German Writing Coach adds 200+ CEFR-matched writing prompts for German exam practice and Clean Entry tracking to measure your writing progress.",
            PublishedDate: new DateOnly(2026, 3, 14),
            ContentComponent: typeof(Client.Components.NewsEntries.NewsEntry_WritingPromptsAndCleanEntries),
            Keywords: "German writing prompts, CEFR writing exercises, German exam writing practice, Goethe B2 C1 writing, German writing progress tracking"
        ),
        new(
            Slug: "building-a-german-writing-coach",
            Title: "Building a German Writing Coach: From C1 Exam Prep to Open Tool",
            Summary: "How preparing for the Goethe-Institut C1 exam led to an AI-powered writing assistant – and why I'd love your feedback.",
            MetaDescription: "Learn how German Writing Coach was born from real C1 exam preparation and how it helps German learners improve their writing with AI-powered feedback.",
            PublishedDate: new DateOnly(2026, 2, 27),
            ContentComponent: typeof(Client.Components.NewsEntries.NewsEntry_BuildingAGermanWritingCoach),
            Keywords: "German C1 exam, Goethe-Institut C1, German writing practice, AI German writing assistant"
        ),
    };

    public static NewsEntryMeta? GetBySlug(string slug) =>
        Entries.FirstOrDefault(e => string.Equals(e.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
