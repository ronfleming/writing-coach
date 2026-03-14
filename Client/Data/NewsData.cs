using Client.Models;

namespace Client.Data;

public static class NewsData
{
    public static IReadOnlyList<NewsEntryMeta> Entries { get; } = new List<NewsEntryMeta>
    {
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
