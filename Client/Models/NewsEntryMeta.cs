namespace Client.Models;

public record NewsEntryMeta(
    string Slug,
    string Title,
    string Summary,
    string MetaDescription,
    DateOnly PublishedDate,
    Type ContentComponent,
    string? Keywords = null
);
