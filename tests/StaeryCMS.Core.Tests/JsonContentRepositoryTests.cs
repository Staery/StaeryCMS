using StaeryCMS.Core.Models;
using StaeryCMS.Core.Services;
using StaeryCMS.Core.Tests.Fakes;

namespace StaeryCMS.Core.Tests;

public sealed class JsonContentRepositoryTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "staerycms-tests-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_folder, "nested", "content.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task Load_ReturnsNullWhenNothingWasSaved()
    {
        var repository = new JsonContentRepository(FilePath);

        Assert.Null(await repository.LoadAsync());
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsEveryField()
    {
        var repository = new JsonContentRepository(FilePath);
        var item = TestData.Item("Round trip", ContentStatus.Published, "News", "Body **text**");
        item.Summary = "Summary";
        item.Tags = ["one", "two"];
        var library = new ContentLibrary { SiteTitle = "Blog", Items = [item] };

        await repository.SaveAsync(library);
        var loaded = await repository.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("Blog", loaded.SiteTitle);
        var copy = Assert.Single(loaded.Items);
        Assert.Equal(item.Id, copy.Id);
        Assert.Equal(item.Title, copy.Title);
        Assert.Equal(item.Slug, copy.Slug);
        Assert.Equal(item.Summary, copy.Summary);
        Assert.Equal(item.Body, copy.Body);
        Assert.Equal(item.Category, copy.Category);
        Assert.Equal(item.Tags, copy.Tags);
        Assert.Equal(item.Status, copy.Status);
        Assert.Equal(item.CreatedAt, copy.CreatedAt);
        Assert.Equal(item.UpdatedAt, copy.UpdatedAt);
        Assert.Equal(item.PublishedAt, copy.PublishedAt);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public async Task Save_WritesReadableJsonWithEnumNames()
    {
        var repository = new JsonContentRepository(FilePath);

        await repository.SaveAsync(new ContentLibrary { Items = [TestData.Item("Readable", ContentStatus.Archived)] });

        var json = await File.ReadAllTextAsync(FilePath);
        Assert.Contains("\"status\": \"archived\"", json);
        Assert.Contains("\"title\": \"Readable\"", json);
    }

    [Fact]
    public async Task Load_FillsMissingValuesFromHandEditedFiles()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, """{ "siteTitle": null, "items": [ { "title": "Bare", "tags": null } ] }""");

        var loaded = await new JsonContentRepository(FilePath).LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(ContentLibrary.DefaultSiteTitle, loaded.SiteTitle);
        var item = Assert.Single(loaded.Items);
        Assert.Equal("Bare", item.Title);
        Assert.Empty(item.Tags);
        Assert.Equal(string.Empty, item.Body);
    }

    [Fact]
    public async Task Load_MovesCorruptFileAsideInsteadOfLosingIt()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, "{ this is not json");

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => new JsonContentRepository(FilePath).LoadAsync());

        Assert.False(File.Exists(FilePath));
        var backup = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(FilePath)!, "content.json.corrupt-*"));
        Assert.Equal("{ this is not json", await File.ReadAllTextAsync(backup));
        Assert.Contains(backup, error.Message);
    }
}
