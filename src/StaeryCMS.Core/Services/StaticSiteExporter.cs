using System.Globalization;
using System.Net;
using System.Text;
using StaeryCMS.Core.Models;

namespace StaeryCMS.Core.Services;

/// <summary>Result of a static site export.</summary>
public sealed record SiteExportResult(string OutputFolder, string IndexPath, int PageCount);

/// <summary>Publishes content as a self-contained static HTML site (index page, one page per entry, stylesheet).</summary>
public sealed class StaticSiteExporter
{
    public const string IndexFileName = "index.html";
    public const string StylesheetFileName = "style.css";

    private static readonly CultureInfo DateCulture = CultureInfo.InvariantCulture;

    /// <summary>Writes every <see cref="ContentStatus.Published"/> entry, newest first, into <paramref name="outputFolder"/>.</summary>
    public async Task<SiteExportResult> ExportAsync(
        ContentLibrary library,
        string outputFolder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);

        Directory.CreateDirectory(outputFolder);

        var published = library.Items
            .Where(item => item.Status == ContentStatus.Published)
            .OrderByDescending(PublicationDate)
            .ToList();

        await WriteAsync(Path.Combine(outputFolder, StylesheetFileName), Stylesheet, cancellationToken).ConfigureAwait(false);

        foreach (var item in published)
        {
            var html = RenderArticlePage(item, library.SiteTitle, linkHome: true);
            await WriteAsync(Path.Combine(outputFolder, PageFileName(item)), html, cancellationToken).ConfigureAwait(false);
        }

        var indexPath = Path.Combine(outputFolder, IndexFileName);
        await WriteAsync(indexPath, RenderIndexPage(published, library.SiteTitle), cancellationToken).ConfigureAwait(false);

        return new SiteExportResult(Path.GetFullPath(outputFolder), Path.GetFullPath(indexPath), published.Count);
    }

    /// <summary>Renders a single entry as a standalone page with the stylesheet inlined, regardless of its status.</summary>
    public string RenderPreview(ContentItem item, string siteTitle)
    {
        ArgumentNullException.ThrowIfNull(item);
        return RenderArticlePage(item, siteTitle, linkHome: false, inlineStyles: true);
    }

    public static string PageFileName(ContentItem item) =>
        (SlugGenerator.IsValid(item.Slug) ? item.Slug : SlugGenerator.Generate(item.Title)) + ".html";

    private static DateTimeOffset PublicationDate(ContentItem item) => item.PublishedAt ?? item.UpdatedAt;

    private static string RenderIndexPage(IReadOnlyList<ContentItem> items, string siteTitle)
    {
        var body = new StringBuilder();
        body.AppendLine($"<header class=\"site-header\"><h1>{Encode(siteTitle)}</h1></header>");
        body.AppendLine("<main class=\"entries\">");

        if (items.Count == 0)
        {
            body.AppendLine("<p class=\"empty\">Nothing has been published yet.</p>");
        }

        foreach (var item in items)
        {
            body.AppendLine("<article class=\"entry\">");
            body.AppendLine($"  <h2><a href=\"{Encode(PageFileName(item))}\">{Encode(item.Title)}</a></h2>");
            body.AppendLine($"  {RenderMeta(item)}");

            if (!string.IsNullOrWhiteSpace(item.Summary))
            {
                body.AppendLine($"  <p>{Encode(item.Summary)}</p>");
            }

            body.AppendLine("</article>");
        }

        body.AppendLine("</main>");
        body.AppendLine($"<footer>Generated with StaeryCMS · {items.Count} {(items.Count == 1 ? "entry" : "entries")}</footer>");

        return Layout(siteTitle, siteTitle, body.ToString(), inlineStyles: false);
    }

    private static string RenderArticlePage(ContentItem item, string siteTitle, bool linkHome, bool inlineStyles = false)
    {
        var body = new StringBuilder();
        body.AppendLine(linkHome
            ? $"<header class=\"site-header\"><a href=\"{IndexFileName}\">← {Encode(siteTitle)}</a></header>"
            : $"<header class=\"site-header\"><span>{Encode(siteTitle)}</span></header>");
        body.AppendLine("<main><article class=\"post\">");
        body.AppendLine($"<h1>{Encode(item.Title)}</h1>");
        body.AppendLine(RenderMeta(item));

        if (item.Tags.Count > 0)
        {
            body.Append("<ul class=\"tags\">");
            foreach (var tag in item.Tags)
            {
                body.Append($"<li>#{Encode(tag)}</li>");
            }

            body.AppendLine("</ul>");
        }

        body.AppendLine("<div class=\"content\">");
        body.AppendLine(MarkdownRenderer.ToHtml(item.Body));
        body.AppendLine("</div></article></main>");

        return Layout($"{item.Title} · {siteTitle}", item.Summary, body.ToString(), inlineStyles);
    }

    private static string RenderMeta(ContentItem item)
    {
        var parts = new List<string>
        {
            $"<time datetime=\"{PublicationDate(item):yyyy-MM-dd}\">{PublicationDate(item).ToString("d MMMM yyyy", DateCulture)}</time>",
        };

        if (!string.IsNullOrWhiteSpace(item.Category))
        {
            parts.Add($"<span class=\"category\">{Encode(item.Category)}</span>");
        }

        var minutes = TextMetrics.EstimateReadingMinutes(TextMetrics.CountWords(item.Body));
        if (minutes > 0)
        {
            parts.Add($"<span>{minutes} min read</span>");
        }

        return $"<p class=\"meta\">{string.Join(" · ", parts)}</p>";
    }

    private static string Layout(string title, string description, string body, bool inlineStyles)
    {
        var styles = inlineStyles
            ? $"<style>{Stylesheet}</style>"
            : $"<link rel=\"stylesheet\" href=\"{StylesheetFileName}\">";

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="generator" content="StaeryCMS">
            <meta name="description" content="{Encode(description)}">
            <title>{Encode(title)}</title>
            {styles}
            </head>
            <body>
            {body}
            </body>
            </html>
            """;
    }

    private static string Encode(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    private static Task WriteAsync(string path, string content, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);

    private const string Stylesheet = """
        :root { --accent: #6366f1; --text: #1f2937; --muted: #6b7280; --bg: #f9fafb; --card: #ffffff; --border: #e5e7eb; }
        * { box-sizing: border-box; }
        body { margin: 0; font-family: "Segoe UI", system-ui, -apple-system, sans-serif; color: var(--text); background: var(--bg); line-height: 1.65; }
        .site-header { padding: 24px max(16px, calc(50% - 360px)); background: #111827; color: #fff; }
        .site-header h1 { margin: 0; font-size: 1.75rem; }
        .site-header a, .site-header span { color: #c7d2fe; text-decoration: none; font-weight: 600; }
        main { max-width: 720px; margin: 32px auto; padding: 0 16px; }
        .entry, .post { background: var(--card); border: 1px solid var(--border); border-radius: 12px; padding: 24px; margin-bottom: 16px; }
        .entry h2 { margin: 0 0 4px; font-size: 1.35rem; }
        .entry h2 a { color: var(--text); text-decoration: none; }
        .entry h2 a:hover { color: var(--accent); }
        .meta { color: var(--muted); font-size: .9rem; margin: 0 0 12px; }
        .category { color: var(--accent); font-weight: 600; }
        .tags { list-style: none; padding: 0; margin: 0 0 16px; display: flex; flex-wrap: wrap; gap: 8px; }
        .tags li { background: #eef2ff; color: #4338ca; border-radius: 999px; padding: 2px 10px; font-size: .85rem; }
        .content img { max-width: 100%; }
        .content pre { background: #111827; color: #e5e7eb; padding: 16px; border-radius: 8px; overflow-x: auto; }
        .content code { font-family: "Cascadia Code", Consolas, monospace; font-size: .9em; }
        .content blockquote { margin: 0; padding-left: 16px; border-left: 4px solid var(--accent); color: var(--muted); }
        .content table { border-collapse: collapse; } .content th, .content td { border: 1px solid var(--border); padding: 6px 12px; }
        .empty { color: var(--muted); text-align: center; }
        footer { text-align: center; color: var(--muted); font-size: .85rem; padding: 24px 16px 48px; }
        """;
}
