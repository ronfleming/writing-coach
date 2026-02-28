using Client.Models;

namespace Client.Data;

public static class NewsData
{
    public static IReadOnlyList<NewsEntryMeta> Entries { get; } = new List<NewsEntryMeta>
    {
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
