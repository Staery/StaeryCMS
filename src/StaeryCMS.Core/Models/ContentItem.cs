namespace StaeryCMS.Core.Models;

/// <summary>A single piece of content (article, page, note) managed by the CMS.</summary>
public sealed class ContentItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    /// <summary>URL-friendly identifier, unique across the site. Used as the exported file name.</summary>
    public string Slug { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    /// <summary>Body text in Markdown.</summary>
    public string Body { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public ContentStatus Status { get; set; } = ContentStatus.Draft;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Set the first time the entry is published; preserved if it is later archived.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    public ContentItem Clone() => new()
    {
        Id = Id,
        Title = Title,
        Slug = Slug,
        Summary = Summary,
        Body = Body,
        Category = Category,
        Tags = [.. Tags],
        Status = Status,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        PublishedAt = PublishedAt,
    };
}
