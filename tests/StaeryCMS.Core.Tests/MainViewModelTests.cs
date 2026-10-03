using StaeryCMS.Core.Abstractions;
using StaeryCMS.Core.Models;
using StaeryCMS.Core.Services;
using StaeryCMS.Core.Tests.Fakes;
using StaeryCMS.Core.ViewModels;

namespace StaeryCMS.Core.Tests;

public class MainViewModelTests
{
    private readonly FakeDialogs _dialogs = new();
    private readonly FakeShell _shell = new();
    private readonly FixedTimeProvider _time = new(TestData.Now);

    private MainViewModel CreateViewModel(InMemoryRepository repository) =>
        new(repository, new StaticSiteExporter(), _dialogs, _shell, _time);

    private async Task<(MainViewModel Vm, InMemoryRepository Repository)> LoadAsync(params ContentItem[] items)
    {
        var repository = new InMemoryRepository(new ContentLibrary { Items = [.. items] });
        var vm = CreateViewModel(repository);
        await vm.LoadCommand.ExecuteAsync(null);
        return (vm, repository);
    }

    [Fact]
    public async Task Load_SeedsSampleContentOnFirstRun()
    {
        var repository = new InMemoryRepository();
        var vm = CreateViewModel(repository);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.Items);
        Assert.NotNull(repository.Stored);
        Assert.Equal(vm.Items.Count, vm.Statistics.Total);
        Assert.Equal(vm.Statistics.Total, vm.StatusFilters[0].Count);
    }

    [Fact]
    public async Task Load_ComputesStatisticsAndCategories()
    {
        var (vm, _) = await LoadAsync(
            TestData.Item("A", ContentStatus.Published, "News", "one two three"),
            TestData.Item("B", ContentStatus.Draft, "guides"),
            TestData.Item("C", ContentStatus.Archived, "news"));

        Assert.Equal(new ContentStatistics(3, 1, 1, 1, 3), vm.Statistics);
        Assert.Equal(["guides", "News"], vm.Categories);
        Assert.Equal([MainViewModel.AllCategories, "guides", "News"], vm.CategoryFilters);
        Assert.Equal([3, 1, 1, 1], vm.StatusFilters.Select(filter => filter.Count));
    }

    [Fact]
    public async Task Filters_CombineStatusCategoryAndSearch()
    {
        var (vm, _) = await LoadAsync(
            TestData.Item("WPF tips", ContentStatus.Published, "Dev"),
            TestData.Item("WPF draft", ContentStatus.Draft, "Dev"),
            TestData.Item("Holiday", ContentStatus.Published, "Life", "photos from the trip to WPF land"));

        vm.SelectedStatusFilter = vm.StatusFilters.Single(filter => filter.Status == ContentStatus.Published);
        Assert.Equal(["Holiday", "WPF tips"], vm.Items.Select(item => item.Title).Order());

        vm.SearchText = "wpf";
        Assert.Equal(2, vm.Items.Count);

        vm.SelectedCategoryFilter = "Dev";
        Assert.Equal("WPF tips", Assert.Single(vm.Items).Title);
        Assert.True(vm.HasActiveFilters);
        Assert.Equal("1 of 3 entries", vm.ResultsText);

        vm.ClearFiltersCommand.Execute(null);
        Assert.Equal(3, vm.Items.Count);
        Assert.False(vm.HasActiveFilters);
    }

    [Fact]
    public async Task Sort_ByTitle()
    {
        var (vm, _) = await LoadAsync(TestData.Item("b"), TestData.Item("C"), TestData.Item("a"));

        vm.SelectedSortOption = vm.SortOptions.Single(option => option.Order == ContentSortOrder.Title);

        Assert.Equal(["a", "b", "C"], vm.Items.Select(item => item.Title));
    }

    [Fact]
    public async Task NewEntry_SaveAddsItAndPersists()
    {
        var (vm, repository) = await LoadAsync();

        await vm.NewEntryCommand.ExecuteAsync(null);
        var editor = Assert.IsType<ContentEditorViewModel>(vm.Editor);
        Assert.True(editor.IsNew);
        Assert.False(vm.SaveCommand.CanExecute(null));

        editor.Title = "Мой первый пост";
        editor.Body = "Hello world";
        editor.Status = ContentStatus.Published;

        Assert.Equal("moy-pervyy-post", editor.Slug);
        Assert.True(vm.SaveCommand.CanExecute(null));

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(repository.Stored!.Items);
        Assert.Equal("Мой первый пост", saved.Title);
        Assert.Equal(TestData.Now, saved.PublishedAt);
        Assert.Equal(TestData.Now, saved.CreatedAt);
        Assert.Equal(saved.Id, vm.SelectedItem?.Id);
        Assert.False(editor.IsDirty);
        Assert.False(editor.IsNew);
        Assert.Equal(1, vm.Statistics.Published);
    }

    [Fact]
    public async Task Editor_RejectsDuplicateSlug()
    {
        var (vm, _) = await LoadAsync(TestData.Item("Taken"));

        await vm.NewEntryCommand.ExecuteAsync(null);
        vm.Editor!.Title = "Taken";

        Assert.Contains("already used", vm.Editor.ValidationError);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Editor_SlugStopsFollowingTitleAfterManualEdit()
    {
        var (vm, _) = await LoadAsync();
        await vm.NewEntryCommand.ExecuteAsync(null);
        var editor = vm.Editor!;

        editor.Title = "First";
        editor.Slug = "custom";
        editor.Title = "Second";

        Assert.Equal("custom", editor.Slug);
    }

    [Fact]
    public async Task SelectingAnotherEntry_WithUnsavedChanges_CanBeCancelled()
    {
        var first = TestData.Item("First");
        var second = TestData.Item("Second");
        var (vm, _) = await LoadAsync(first, second);

        vm.SelectedItem = vm.Items.Single(item => item.Id == first.Id);
        vm.Editor!.Body = "unsaved";
        _dialogs.UnsavedChangesDecision = UnsavedChangesDecision.Cancel;

        vm.SelectedItem = vm.Items.Single(item => item.Id == second.Id);

        Assert.Equal(1, _dialogs.SavePrompts);
        Assert.Equal(first.Id, vm.Editor.Id);
        Assert.Equal(first.Id, vm.SelectedItem?.Id);
        Assert.Equal("unsaved", vm.Editor.Body);
    }

    [Fact]
    public async Task SelectingAnotherEntry_WithUnsavedChanges_CanSaveFirst()
    {
        var first = TestData.Item("First");
        var second = TestData.Item("Second");
        var (vm, repository) = await LoadAsync(first, second);

        vm.SelectedItem = vm.Items.Single(item => item.Id == first.Id);
        vm.Editor!.Body = "saved on switch";
        _dialogs.UnsavedChangesDecision = UnsavedChangesDecision.Save;

        vm.SelectedItem = vm.Items.Single(item => item.Id == second.Id);

        Assert.Equal(second.Id, vm.Editor!.Id);
        Assert.Equal(second.Id, vm.SelectedItem?.Id);
        Assert.Equal("saved on switch", repository.Stored!.Items.Single(item => item.Id == first.Id).Body);
    }

    [Fact]
    public async Task SelectingAnotherEntry_WithUnsavedChanges_CanDiscard()
    {
        var first = TestData.Item("First", body: "original");
        var second = TestData.Item("Second");
        var (vm, _) = await LoadAsync(first, second);

        vm.SelectedItem = vm.Items.Single(item => item.Id == first.Id);
        vm.Editor!.Body = "throw away";
        _dialogs.UnsavedChangesDecision = UnsavedChangesDecision.Discard;

        vm.SelectedItem = vm.Items.Single(item => item.Id == second.Id);

        Assert.Equal(second.Id, vm.Editor!.Id);
        Assert.Equal("original", first.Body);
    }

    [Fact]
    public async Task Revert_RestoresStoredValues()
    {
        var item = TestData.Item("Keep me", body: "original");
        var (vm, _) = await LoadAsync(item);
        vm.SelectedItem = vm.Items[0];

        vm.Editor!.Title = "Changed";
        vm.Editor.Body = "changed";
        vm.RevertCommand.Execute(null);

        Assert.Equal("Keep me", vm.Editor.Title);
        Assert.Equal("original", vm.Editor.Body);
        Assert.False(vm.Editor.IsDirty);
    }

    [Fact]
    public async Task Delete_RemovesEntryAfterConfirmation()
    {
        var keep = TestData.Item("Keep");
        var remove = TestData.Item("Remove");
        var (vm, repository) = await LoadAsync(keep, remove);
        vm.SelectedItem = vm.Items.Single(item => item.Id == remove.Id);

        _dialogs.ConfirmResult = false;
        await vm.DeleteCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Items.Count);

        _dialogs.ConfirmResult = true;
        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(keep.Id, Assert.Single(vm.Items).Id);
        Assert.Equal(keep.Id, Assert.Single(repository.Stored!.Items).Id);
        Assert.Null(vm.Editor);
        Assert.Null(vm.SelectedItem);
    }

    [Fact]
    public async Task Delete_KeepsEntryWhenSavingFails()
    {
        var (vm, repository) = await LoadAsync(TestData.Item("Precious"));
        vm.SelectedItem = vm.Items[0];
        repository.FailSaves = true;

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Single(vm.Items);
        Assert.Single(repository.Stored!.Items);
        Assert.NotNull(vm.Editor);
        Assert.Single(_dialogs.Errors);
    }

    [Fact]
    public async Task Save_FailureKeepsEditorDirtySoUserCanRetry()
    {
        var (vm, repository) = await LoadAsync(TestData.Item("Entry"));
        vm.SelectedItem = vm.Items[0];
        vm.Editor!.Body = "new text";
        repository.FailSaves = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.Editor.IsDirty);
        Assert.True(vm.SaveCommand.CanExecute(null));

        repository.FailSaves = false;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("new text", repository.Stored!.Items[0].Body);
    }

    [Fact]
    public async Task Duplicate_CreatesUnsavedDraftWithUniqueSlug()
    {
        var original = TestData.Item("Original", ContentStatus.Published, "News", "body");
        var (vm, repository) = await LoadAsync(original);
        vm.SelectedItem = vm.Items[0];

        await vm.DuplicateCommand.ExecuteAsync(null);

        var copy = vm.Editor!;
        Assert.True(copy.IsNew);
        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal("Original (copy)", copy.Title);
        Assert.Equal("original-copy", copy.Slug);
        Assert.Equal(ContentStatus.Draft, copy.Status);
        Assert.Equal("body", copy.Body);
        Assert.Single(repository.Stored!.Items);
    }

    [Fact]
    public async Task SavingEntryHiddenByStatusFilter_SwitchesToAllEntries()
    {
        var (vm, _) = await LoadAsync(TestData.Item("Draft one"));
        vm.SelectedStatusFilter = vm.StatusFilters.Single(filter => filter.Status == ContentStatus.Draft);
        vm.SelectedItem = vm.Items[0];

        vm.Editor!.Status = ContentStatus.Published;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(vm.SelectedStatusFilter.Status);
        Assert.Equal(vm.Editor.Id, vm.SelectedItem?.Id);
    }

    [Fact]
    public async Task Preview_RendersUnsavedChanges()
    {
        var (vm, _) = await LoadAsync(TestData.Item("Preview me"));
        vm.SelectedItem = vm.Items[0];
        vm.Editor!.Body = "**Fresh** text";

        vm.PreviewCommand.Execute(null);

        Assert.Contains("<strong>Fresh</strong>", _shell.LastHtml);
    }

    [Fact]
    public async Task ExportSite_WritesFilesAndOffersToOpenThem()
    {
        var folder = Path.Combine(Path.GetTempPath(), "staerycms-vm-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, repository) = await LoadAsync(TestData.Item("Public", ContentStatus.Published));
            vm.SiteTitle = "Renamed site";
            _dialogs.FolderToPick = folder;

            await vm.ExportSiteCommand.ExecuteAsync(null);

            Assert.Equal("Renamed site", repository.Stored!.SiteTitle);
            Assert.True(File.Exists(Path.Combine(folder, "public.html")));
            Assert.Equal(Path.Combine(folder, StaticSiteExporter.IndexFileName), Assert.Single(_shell.Opened));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PrepareToClose_AsksAboutUnsavedChanges()
    {
        var (vm, _) = await LoadAsync(TestData.Item("Entry"));
        vm.SelectedItem = vm.Items[0];
        vm.Editor!.Body = "unsaved";

        _dialogs.UnsavedChangesDecision = UnsavedChangesDecision.Cancel;
        Assert.False(await vm.PrepareToCloseAsync());

        _dialogs.UnsavedChangesDecision = UnsavedChangesDecision.Discard;
        Assert.True(await vm.PrepareToCloseAsync());
    }
}
