using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StaeryCMS.Core.Abstractions;
using StaeryCMS.Core.Models;
using StaeryCMS.Core.Services;

namespace StaeryCMS.Core.ViewModels;

/// <summary>State and commands of the main window: navigation, filtering, the content list and the editor.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    public const string AllCategories = "All categories";

    private readonly IContentRepository _repository;
    private readonly StaticSiteExporter _exporter;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly TimeProvider _time;
    private readonly List<ContentListItemViewModel> _entries = [];

    private ContentLibrary _library = new();
    private bool _isRebuilding;
    private bool _siteSettingsDirty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private StatusFilterOption _selectedStatusFilter;

    [ObservableProperty]
    private string? _selectedCategoryFilter = AllCategories;

    [ObservableProperty]
    private SortOption _selectedSortOption;

    [ObservableProperty]
    private ContentListItemViewModel? _selectedItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditor))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(RevertCommand), nameof(DeleteCommand), nameof(DuplicateCommand), nameof(PreviewCommand))]
    private ContentEditorViewModel? _editor;

    [ObservableProperty]
    private ContentStatistics _statistics = ContentStatistics.Empty;

    [ObservableProperty]
    private string _siteTitle = ContentLibrary.DefaultSiteTitle;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportSiteCommand))]
    private bool _isBusy;

    public MainViewModel(
        IContentRepository repository,
        StaticSiteExporter exporter,
        IDialogService dialogs,
        IShellService shell,
        TimeProvider time)
    {
        _repository = repository;
        _exporter = exporter;
        _dialogs = dialogs;
        _shell = shell;
        _time = time;

        StatusFilters =
        [
            new StatusFilterOption("All entries", "", null),
            new StatusFilterOption("Published", "", ContentStatus.Published),
            new StatusFilterOption("Drafts", "", ContentStatus.Draft),
            new StatusFilterOption("Archived", "", ContentStatus.Archived),
        ];

        SortOptions =
        [
            new SortOption("Recently updated", ContentSortOrder.RecentlyUpdated),
            new SortOption("Recently created", ContentSortOrder.RecentlyCreated),
            new SortOption("Title (A–Z)", ContentSortOrder.Title),
        ];

        _selectedStatusFilter = StatusFilters[0];
        _selectedSortOption = SortOptions[0];
    }

    public IReadOnlyList<StatusFilterOption> StatusFilters { get; }

    public IReadOnlyList<SortOption> SortOptions { get; }

    /// <summary>Entries matching the current filters, in display order.</summary>
    public ObservableCollection<ContentListItemViewModel> Items { get; } = [];

    /// <summary>Known categories, used as suggestions in the editor.</summary>
    public ObservableCollection<string> Categories { get; } = [];

    /// <summary>Known categories preceded by <see cref="AllCategories"/>, used by the list filter.</summary>
    public ObservableCollection<string> CategoryFilters { get; } = [AllCategories];

    public bool HasEditor => Editor is not null;

    public bool IsListEmpty => Items.Count == 0;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchText) || SelectedStatusFilter.Status is not null || !IsAllCategories(SelectedCategoryFilter);

    public string ResultsText => Items.Count == _entries.Count
        ? $"{_entries.Count} {Plural(_entries.Count, "entry", "entries")}"
        : $"{Items.Count} of {_entries.Count} {Plural(_entries.Count, "entry", "entries")}";

    public string StorageLocation => _repository.Location;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var library = await _repository.LoadAsync();

            if (library is null)
            {
                library = SampleContent.Create(_time.GetLocalNow());
                await _repository.SaveAsync(library);
                StatusMessage = "Welcome! A few sample entries were created to get you started.";
            }
            else
            {
                StatusMessage = $"Loaded {library.Items.Count} {Plural(library.Items.Count, "entry", "entries")} from {_repository.Location}";
            }

            SetLibrary(library);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _dialogs.ShowError("Could not load content", ex.Message);
            SetLibrary(new ContentLibrary());
            StatusMessage = "Started with an empty library.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NewEntryAsync()
    {
        if (!await ConfirmLeaveEditorAsync())
        {
            return;
        }

        var now = _time.GetLocalNow();
        var item = new ContentItem { CreatedAt = now, UpdatedAt = now };

        // A new entry is shown in every status view, so keep it visible if a filter hides drafts.
        if (SelectedStatusFilter.Status is not null and not ContentStatus.Draft)
        {
            SelectedStatusFilter = StatusFilters[0];
        }

        SelectWithoutPrompt(null);
        Editor = CreateEditor(item, isNew: true);
        StatusMessage = "New draft — give it a title and press Save.";
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveEditorAsync();

    private bool CanSave() => Editor?.CanSave == true;

    [RelayCommand(CanExecute = nameof(CanRevert))]
    private void Revert()
    {
        Editor!.Revert();
        StatusMessage = "Changes discarded.";
    }

    private bool CanRevert() => Editor?.IsDirty == true;

    [RelayCommand(CanExecute = nameof(HasEditor))]
    private async Task DeleteAsync()
    {
        var editor = Editor!;
        var title = string.IsNullOrWhiteSpace(editor.Title) ? "this entry" : $"\"{editor.Title}\"";

        if (!_dialogs.Confirm("Delete entry", $"Delete {title}? This cannot be undone."))
        {
            return;
        }

        if (!editor.IsNew)
        {
            var index = _library.Items.FindIndex(item => item.Id == editor.Id);
            var item = _library.Items[index];
            _library.Items.RemoveAt(index);

            if (!await PersistAsync())
            {
                _library.Items.Insert(index, item);
                return;
            }

            _entries.RemoveAll(entry => entry.Id == editor.Id);
        }

        Editor = null;
        SelectWithoutPrompt(null);
        RefreshAfterLibraryChange();
        StatusMessage = $"Deleted {title}.";
    }

    [RelayCommand(CanExecute = nameof(CanDuplicate))]
    private async Task DuplicateAsync()
    {
        if (!await ConfirmLeaveEditorAsync() || Editor is null)
        {
            return;
        }

        var now = _time.GetLocalNow();
        var copy = Editor.CreateSnapshot(now);
        copy.Id = Guid.NewGuid();
        copy.Title = $"{copy.Title} (copy)";
        copy.Slug = SlugGenerator.MakeUnique(SlugGenerator.Generate(copy.Title), TakenSlugs());
        copy.Status = ContentStatus.Draft;
        copy.CreatedAt = now;
        copy.UpdatedAt = now;
        copy.PublishedAt = null;

        SelectWithoutPrompt(null);
        Editor = CreateEditor(copy, isNew: true);
        Editor.Revalidate();
        StatusMessage = "Duplicated as a new draft. Save it to keep the copy.";
    }

    private bool CanDuplicate() => Editor is { IsNew: false };

    [RelayCommand(CanExecute = nameof(HasEditor))]
    private void Preview()
    {
        var snapshot = Editor!.CreateSnapshot(_time.GetLocalNow());

        try
        {
            _shell.ShowHtml(_exporter.RenderPreview(snapshot, SiteTitle), StaticSiteExporter.PageFileName(snapshot));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _dialogs.ShowError("Preview failed", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportSiteAsync()
    {
        if (Editor?.IsDirty == true && !await ConfirmLeaveEditorAsync())
        {
            return;
        }

        var folder = _dialogs.PickFolder("Choose a folder for the exported site");
        if (folder is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (_siteSettingsDirty && !await PersistAsync())
            {
                return;
            }

            var result = await _exporter.ExportAsync(_library, folder);
            StatusMessage = $"Exported {result.PageCount} {Plural(result.PageCount, "page", "pages")} to {result.OutputFolder}";

            if (_dialogs.Confirm("Export complete", $"{StatusMessage}.\n\nOpen the site in your browser?"))
            {
                _shell.Open(result.IndexPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _dialogs.ShowError("Export failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanExport() => !IsBusy;

    [RelayCommand]
    private void ClearFilters()
    {
        _isRebuilding = true;
        try
        {
            SearchText = string.Empty;
            SelectedStatusFilter = StatusFilters[0];
            SelectedCategoryFilter = AllCategories;
        }
        finally
        {
            _isRebuilding = false;
        }

        RebuildList();
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        var folder = Path.GetDirectoryName(_repository.Location);
        if (folder is null || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            _shell.Open(folder);
        }
        catch (InvalidOperationException ex)
        {
            _dialogs.ShowError("Could not open folder", ex.Message);
        }
    }

    /// <summary>Called when the window is about to close. Returns <see langword="false"/> to keep it open.</summary>
    public async Task<bool> PrepareToCloseAsync()
    {
        if (!await ConfirmLeaveEditorAsync())
        {
            return false;
        }

        return !_siteSettingsDirty || await PersistAsync();
    }

    partial void OnSearchTextChanged(string value) => RebuildList();

    partial void OnSelectedStatusFilterChanged(StatusFilterOption value)
    {
        // A ListBox may briefly push null while its items are refreshed.
        if (value is null)
        {
            SelectedStatusFilter = StatusFilters[0];
            return;
        }

        RebuildList();
    }

    partial void OnSelectedCategoryFilterChanged(string? value) => RebuildList();

    partial void OnSelectedSortOptionChanged(SortOption value) => RebuildList();

    partial void OnSiteTitleChanged(string value)
    {
        if (_library.SiteTitle != value)
        {
            _library.SiteTitle = value;
            _siteSettingsDirty = true;
        }
    }

    partial void OnSelectedItemChanged(ContentListItemViewModel? oldValue, ContentListItemViewModel? newValue)
    {
        if (_isRebuilding || newValue is null || newValue.Id == Editor?.Id)
        {
            return;
        }

        _ = SwitchToAsync(newValue, oldValue);
    }

    partial void OnEditorChanged(ContentEditorViewModel? oldValue, ContentEditorViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.CanSaveChanged -= OnEditorCanSaveChanged;
        }

        if (newValue is not null)
        {
            newValue.CanSaveChanged += OnEditorCanSaveChanged;
        }
    }

    private void OnEditorCanSaveChanged(object? sender, EventArgs e)
    {
        SaveCommand.NotifyCanExecuteChanged();
        RevertCommand.NotifyCanExecuteChanged();
    }

    private async Task SwitchToAsync(ContentListItemViewModel target, ContentListItemViewModel? previous)
    {
        if (await ConfirmLeaveEditorAsync())
        {
            Editor = CreateEditor(target.Model, isNew: false);

            // Saving may have rebuilt the list, replacing the row the user clicked.
            SelectWithoutPrompt(Items.FirstOrDefault(item => item.Id == target.Id));
        }
        else
        {
            // Defer so the list control finishes its own selection change before we undo it.
            RunAfterCurrentUpdate(() => SelectWithoutPrompt(previous));
        }
    }

    /// <summary>Asks what to do with unsaved edits. Returns <see langword="true"/> if it is safe to move on.</summary>
    private async Task<bool> ConfirmLeaveEditorAsync()
    {
        if (Editor is not { IsDirty: true } editor)
        {
            return true;
        }

        var title = string.IsNullOrWhiteSpace(editor.Title) ? "Untitled" : editor.Title;

        switch (_dialogs.AskToSaveChanges(title))
        {
            case UnsavedChangesDecision.Discard:
                editor.Revert();
                if (editor.IsNew)
                {
                    Editor = null;
                }

                return true;

            case UnsavedChangesDecision.Save:
                if (editor.ValidationError is { } error)
                {
                    _dialogs.ShowError("Cannot save", error);
                    return false;
                }

                return await SaveEditorAsync();

            default:
                return false;
        }
    }

    private async Task<bool> SaveEditorAsync()
    {
        var editor = Editor;
        if (editor is null || editor.ValidationError is not null)
        {
            return false;
        }

        var wasNew = editor.IsNew;
        var item = editor.Commit(_time.GetLocalNow());

        ContentListItemViewModel row;
        if (wasNew)
        {
            _library.Items.Add(item);
            row = new ContentListItemViewModel(item);
            _entries.Add(row);
        }
        else
        {
            row = _entries.First(entry => entry.Id == item.Id);
            row.Refresh();
        }

        if (!await PersistAsync())
        {
            // The values are committed in memory only; keep Save enabled so the user can retry.
            editor.MarkDirty();
            return false;
        }

        // Make sure the saved entry stays visible even if it no longer matches the status filter.
        if (!SelectedStatusFilter.Includes(item.Status))
        {
            _isRebuilding = true;
            SelectedStatusFilter = StatusFilters[0];
            _isRebuilding = false;
        }

        RefreshAfterLibraryChange();
        SelectWithoutPrompt(Items.FirstOrDefault(entry => entry.Id == item.Id));
        StatusMessage = $"Saved \"{item.Title}\" at {_time.GetLocalNow():HH:mm}.";
        return true;
    }

    private async Task<bool> PersistAsync()
    {
        try
        {
            await _repository.SaveAsync(_library);
            _siteSettingsDirty = false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Could not save", ex.Message);
            StatusMessage = "Saving failed — nothing was written to disk.";
            return false;
        }
    }

    private void SetLibrary(ContentLibrary library)
    {
        _library = library;
        _entries.Clear();
        _entries.AddRange(library.Items.Select(item => new ContentListItemViewModel(item)));

        _isRebuilding = true;
        SiteTitle = library.SiteTitle;
        _isRebuilding = false;
        _siteSettingsDirty = false;

        Editor = null;
        RefreshAfterLibraryChange();
    }

    private ContentEditorViewModel CreateEditor(ContentItem item, bool isNew) =>
        new(item, (slug, id) => _library.Items.Any(other => other.Id != id && string.Equals(other.Slug, slug, StringComparison.OrdinalIgnoreCase)), isNew);

    private HashSet<string> TakenSlugs() =>
        new(_library.Items.Select(item => item.Slug), StringComparer.OrdinalIgnoreCase);

    private void RefreshAfterLibraryChange()
    {
        Statistics = ContentStatistics.From(_library.Items);

        foreach (var filter in StatusFilters)
        {
            filter.Count = _library.Items.Count(item => filter.Includes(item.Status));
        }

        RefreshCategories();
        RebuildList();
    }

    private void RefreshCategories()
    {
        var categories = _library.Items
            .Select(item => item.Category.Trim())
            .Where(category => category.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (categories.SequenceEqual(Categories))
        {
            return;
        }

        var selected = SelectedCategoryFilter;

        _isRebuilding = true;
        try
        {
            Categories.Clear();
            CategoryFilters.Clear();
            CategoryFilters.Add(AllCategories);

            foreach (var category in categories)
            {
                Categories.Add(category);
                CategoryFilters.Add(category);
            }

            SelectedCategoryFilter = CategoryFilters.Contains(selected ?? AllCategories) ? selected : AllCategories;
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    private void RebuildList()
    {
        if (_isRebuilding)
        {
            return;
        }

        var search = SearchText;
        var category = SelectedCategoryFilter;
        var query = _entries.Where(entry =>
            SelectedStatusFilter.Includes(entry.Status) &&
            (IsAllCategories(category) || string.Equals(entry.Category.Trim(), category, StringComparison.CurrentCultureIgnoreCase)) &&
            entry.Matches(search));

        query = SelectedSortOption.Order switch
        {
            ContentSortOrder.RecentlyCreated => query.OrderByDescending(entry => entry.Model.CreatedAt),
            ContentSortOrder.Title => query.OrderBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => query.OrderByDescending(entry => entry.UpdatedAt),
        };

        var selectedId = Editor?.Id;

        _isRebuilding = true;
        try
        {
            Items.Clear();
            foreach (var entry in query)
            {
                Items.Add(entry);
            }

            SelectedItem = Items.FirstOrDefault(entry => entry.Id == selectedId);
        }
        finally
        {
            _isRebuilding = false;
        }

        OnPropertyChanged(nameof(IsListEmpty));
        OnPropertyChanged(nameof(ResultsText));
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    private void SelectWithoutPrompt(ContentListItemViewModel? item)
    {
        _isRebuilding = true;
        try
        {
            SelectedItem = item;
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    private static void RunAfterCurrentUpdate(Action action)
    {
        if (SynchronizationContext.Current is { } context)
        {
            context.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }

    private static bool IsAllCategories(string? category) => category is null || category == AllCategories;

    private static string Plural(int count, string one, string many) => count == 1 ? one : many;
}
