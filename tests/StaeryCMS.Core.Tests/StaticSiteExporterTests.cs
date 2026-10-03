using StaeryCMS.Core.Models;
using StaeryCMS.Core.Services;
using StaeryCMS.Core.Tests.Fakes;

namespace StaeryCMS.Core.Tests;

public sealed class StaticSiteExporterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "staerycms-export-" + Guid.NewGuid().ToString("N"));
    private readonly StaticSiteExporter _exporter = new();

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task Export_WritesOnlyPublishedEntries()
    {
        var library = new ContentLibrary
        {
            SiteTitle = "Test site",
            Items =
            [
                TestData.Item("Visible post", ContentStatus.Published, body: "# Heading"),
                TestData.Item("Secret draft"),
                TestData.Item("Old news", ContentStatus.Archived),
            ],
        };

        var result = await _exporter.ExportAsync(library, _folder);

        Assert.Equal(1, result.PageCount);
        Assert.True(File.Exists(Path.Combine(_folder, "visible-post.html")));
        Assert.True(File.Exists(Path.Combine(_folder, StaticSiteExporter.StylesheetFileName)));
        Assert.False(File.Exists(Path.Combine(_folder, "secret-draft.html")));
        Assert.False(File.Exists(Path.Combine(_folder, "old-news.html")));

        var index = await File.ReadAllTextAsync(result.IndexPath);
        Assert.Contains("Test site", index);
        Assert.Contains("href=\"visible-post.html\"", index);
        Assert.DoesNotContain("Secret draft", index);

        var page = await File.ReadAllTextAsync(Path.Combine(_folder, "visible-post.html"));
        Assert.Contains("<h1 id=\"heading\">Heading</h1>", page);
    }

    [Fact]
    public async Task Export_ListsNewestPublicationFirst()
    {
        var older = TestData.Item("Older", ContentStatus.Published);
        older.PublishedAt = TestData.Now.AddDays(-10);
        var newer = TestData.Item("Newer", ContentStatus.Published);
        newer.PublishedAt = TestData.Now;

        var result = await _exporter.ExportAsync(new ContentLibrary { Items = [older, newer] }, _folder);

        var index = await File.ReadAllTextAsync(result.IndexPath);
        Assert.True(index.IndexOf("Newer", StringComparison.Ordinal) < index.IndexOf("Older", StringComparison.Ordinal));
    }

    [Fact]
    public void Preview_EscapesHtmlInTitlesAndMarkdown()
    {
        var item = TestData.Item("<script>alert(1)</script>", body: "Hello <img src=x onerror=alert(1)>");

        var html = _exporter.RenderPreview(item, "Site & Co");

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("Site &amp; Co", html);
        Assert.Contains("<style>", html);
    }

    [Fact]
    public async Task Export_UsesGeneratedFileNameForInvalidSlug()
    {
        var item = TestData.Item("Fallback name", ContentStatus.Published);
        item.Slug = "../../evil";

        await _exporter.ExportAsync(new ContentLibrary { Items = [item] }, _folder);

        Assert.True(File.Exists(Path.Combine(_folder, "fallback-name.html")));
    }
}
