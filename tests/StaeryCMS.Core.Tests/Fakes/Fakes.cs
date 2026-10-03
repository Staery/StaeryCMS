using StaeryCMS.Core.Abstractions;
using StaeryCMS.Core.Models;
using StaeryCMS.Core.Services;

namespace StaeryCMS.Core.Tests.Fakes;

internal sealed class InMemoryRepository(ContentLibrary? initial = null) : IContentRepository
{
    public ContentLibrary? Stored { get; private set; } = initial;

    public int SaveCount { get; private set; }

    public bool FailSaves { get; set; }

    public string Location => "memory://content.json";

    public Task<ContentLibrary?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Stored);

    public Task SaveAsync(ContentLibrary library, CancellationToken cancellationToken = default)
    {
        if (FailSaves)
        {
            throw new IOException("Disk is full.");
        }

        SaveCount++;
        Stored = library;
        return Task.CompletedTask;
    }
}

internal sealed class FakeDialogs : IDialogService
{
    public bool ConfirmResult { get; set; } = true;

    public UnsavedChangesDecision UnsavedChangesDecision { get; set; } = UnsavedChangesDecision.Cancel;

    public string? FolderToPick { get; set; }

    public List<string> Errors { get; } = [];

    public int SavePrompts { get; private set; }

    public bool Confirm(string title, string message) => ConfirmResult;

    public UnsavedChangesDecision AskToSaveChanges(string entryTitle)
    {
        SavePrompts++;
        return UnsavedChangesDecision;
    }

    public void ShowError(string title, string message) => Errors.Add(message);

    public string? PickFolder(string title) => FolderToPick;
}

internal sealed class FakeShell : IShellService
{
    public List<string> Opened { get; } = [];

    public string? LastHtml { get; private set; }

    public void Open(string path) => Opened.Add(path);

    public void ShowHtml(string html, string fileNameHint) => LastHtml = html;
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2025, 3, 14, 12, 0, 0, TimeSpan.Zero);

    public static ContentItem Item(string title, ContentStatus status = ContentStatus.Draft, string category = "", string body = "") => new()
    {
        Title = title,
        Slug = SlugGenerator.Generate(title),
        Status = status,
        Category = category,
        Body = body,
        CreatedAt = Now.AddDays(-1),
        UpdatedAt = Now.AddDays(-1),
        PublishedAt = status == ContentStatus.Published ? Now.AddDays(-1) : null,
    };
}
