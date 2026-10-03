using StaeryCMS.Core.Models;

namespace StaeryCMS.Core.Services;

/// <summary>Starter content created on first launch so the app is not empty.</summary>
public static class SampleContent
{
    public static ContentLibrary Create(DateTimeOffset now)
    {
        var welcome = Item(
            now.AddDays(-6),
            "Welcome to StaeryCMS",
            "A quick tour of the editor, statuses and static site export.",
            "Guides",
            ["getting-started", "staerycms"],
            ContentStatus.Published,
            """
            # Welcome!

            **StaeryCMS** is a lightweight desktop content management system.
            Write in *Markdown*, organise entries with categories and tags, then export
            everything that is **published** as a static website.

            ## Workflow

            1. Create an entry with **New** (`Ctrl+N`).
            2. Write the body in Markdown — the word count and reading time update live.
            3. Switch the status to *Published* and press **Save** (`Ctrl+S`).
            4. Use **Export site** to generate HTML you can host anywhere.

            > Tip: the slug follows the title until you edit it by hand.
            """);

        var markdown = Item(
            now.AddDays(-3),
            "Markdown cheat sheet",
            "Everything the exporter understands, from tables to task lists.",
            "Guides",
            ["markdown", "reference"],
            ContentStatus.Published,
            """
            ## Text

            *Italic*, **bold**, ~~strikethrough~~ and `inline code`.

            ## Lists

            - [x] Task lists
            - [ ] Nested items
              - like this one

            ## Code

            ```csharp
            Console.WriteLine("Hello from StaeryCMS!");
            ```

            ## Tables

            | Status    | Exported |
            |-----------|:--------:|
            | Draft     | no       |
            | Published | yes      |
            | Archived  | no       |
            """);

        var roadmap = Item(
            now.AddDays(-1),
            "Roadmap ideas",
            "Features that could come next.",
            "Notes",
            ["planning"],
            ContentStatus.Draft,
            """
            - Image library
            - Scheduled publishing
            - RSS feed in the exported site
            """);

        var legacy = Item(
            now.AddDays(-30),
            "Old announcement",
            "Archived entries stay in the library but are not exported.",
            "News",
            ["archive"],
            ContentStatus.Archived,
            "This post is kept for history.");

        return new ContentLibrary { Items = [welcome, markdown, roadmap, legacy] };
    }

    private static ContentItem Item(
        DateTimeOffset date,
        string title,
        string summary,
        string category,
        List<string> tags,
        ContentStatus status,
        string body) => new()
        {
            Title = title,
            Slug = SlugGenerator.Generate(title),
            Summary = summary,
            Category = category,
            Tags = tags,
            Status = status,
            Body = body,
            CreatedAt = date,
            UpdatedAt = date,
            PublishedAt = status == ContentStatus.Draft ? null : date,
        };
}
