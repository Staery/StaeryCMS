using CommunityToolkit.Mvvm.ComponentModel;
using StaeryCMS.Core.Models;
using StaeryCMS.Core.Services;

namespace StaeryCMS.Core.ViewModels;

/// <summary>
/// Editable working copy of a <see cref="ContentItem"/>. Changes stay here until
/// <see cref="Commit"/> is called, so they can be reverted or discarded.
/// </summary>
public sealed partial class ContentEditorViewModel : ObservableObject
{
    private readonly ContentItem _source;
    private readonly Func<string, Guid, bool> _isSlugTaken;
    private bool _slugFollowsTitle;
    private bool _isLoading;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _slug = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _body = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _tagsText = string.Empty;

    [ObservableProperty]
    private ContentStatus _status;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    private string? _validationError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReadingTimeText))]
    private int _wordCount;

    /// <param name="source">The stored entry; it is only modified by <see cref="Commit"/>.</param>
    /// <param name="isSlugTaken">Returns <see langword="true"/> if another entry (with a different id) already uses the slug.</param>
    /// <param name="isNew">Whether the entry has never been saved.</param>
    public ContentEditorViewModel(ContentItem source, Func<string, Guid, bool> isSlugTaken, bool isNew = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(isSlugTaken);

        _source = source;
        _isSlugTaken = isSlugTaken;
        IsNew = isNew;
        LoadFromSource();
    }

    public Guid Id => _source.Id;

    public bool IsNew { get; private set; }

    public DateTimeOffset CreatedAt => _source.CreatedAt;

    public DateTimeOffset UpdatedAt => _source.UpdatedAt;

    public DateTimeOffset? PublishedAt => _source.PublishedAt;

    public IReadOnlyList<ContentStatus> Statuses { get; } = Enum.GetValues<ContentStatus>();

    public bool HasValidationError => ValidationError is not null;

    public bool CanSave => IsDirty && ValidationError is null;

    public string ReadingTimeText
    {
        get
        {
            var minutes = TextMetrics.EstimateReadingMinutes(WordCount);
            return minutes == 0 ? "empty" : $"{minutes} min read";
        }
    }

    /// <summary>Raised whenever <see cref="CanSave"/> may have changed.</summary>
    public event EventHandler? CanSaveChanged;

    /// <summary>Writes the edited values into the stored entry, resets <see cref="IsDirty"/> and returns the entry.</summary>
    public ContentItem Commit(DateTimeOffset now)
    {
        ApplyValues(_source, now);

        if (_source.CreatedAt == default)
        {
            _source.CreatedAt = now;
        }

        IsNew = false;
        LoadFromSource();
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(CreatedAt));
        OnPropertyChanged(nameof(UpdatedAt));
        OnPropertyChanged(nameof(PublishedAt));
        return _source;
    }

    /// <summary>Snapshot of the current (possibly unsaved) values, used for previews.</summary>
    public ContentItem CreateSnapshot(DateTimeOffset now)
    {
        var snapshot = _source.Clone();
        ApplyValues(snapshot, now);
        return snapshot;
    }

    /// <summary>Flags the entry as needing a save, e.g. after writing it to disk failed.</summary>
    public void MarkDirty() => IsDirty = true;

    /// <summary>Discards unsaved edits.</summary>
    public void Revert() => LoadFromSource();

    /// <summary>Re-runs validation, e.g. after another entry took the slug.</summary>
    public void Revalidate() => ValidationError = Validate();

    partial void OnTitleChanged(string value)
    {
        if (_slugFollowsTitle && !_isLoading)
        {
            _isLoading = true;
            Slug = SlugGenerator.Generate(value);
            _isLoading = false;
        }

        OnEdited();
    }

    partial void OnSlugChanged(string value)
    {
        if (!_isLoading)
        {
            // Once the user types a slug by hand, stop overwriting it from the title.
            _slugFollowsTitle = false;
        }

        OnEdited();
    }

    partial void OnSummaryChanged(string value) => OnEdited();

    partial void OnBodyChanged(string value)
    {
        WordCount = TextMetrics.CountWords(value);
        OnEdited();
    }

    partial void OnCategoryChanged(string value) => OnEdited();

    partial void OnTagsTextChanged(string value) => OnEdited();

    partial void OnStatusChanged(ContentStatus value) => OnEdited();

    partial void OnIsDirtyChanged(bool value) => NotifyCanSaveChanged();

    partial void OnValidationErrorChanged(string? value) => NotifyCanSaveChanged();

    private void ApplyValues(ContentItem target, DateTimeOffset now)
    {
        target.Title = Title.Trim();
        target.Slug = Slug.Trim();
        target.Summary = Summary.Trim();
        target.Body = Body;
        target.Category = Category.Trim();
        target.Tags = TagParser.Parse(TagsText);
        target.Status = Status;
        target.UpdatedAt = now;
        target.PublishedAt ??= Status == ContentStatus.Published ? now : null;
    }

    private void LoadFromSource()
    {
        _isLoading = true;
        try
        {
            Title = _source.Title;
            Slug = _source.Slug;
            Summary = _source.Summary;
            Body = _source.Body;
            Category = _source.Category;
            TagsText = TagParser.Format(_source.Tags);
            Status = _source.Status;
            _slugFollowsTitle = IsNew || string.IsNullOrEmpty(_source.Slug) || _source.Slug == SlugGenerator.Generate(_source.Title);
        }
        finally
        {
            _isLoading = false;
        }

        IsDirty = false;
        ValidationError = Validate();
    }

    private void OnEdited()
    {
        if (_isLoading)
        {
            return;
        }

        IsDirty = true;
        ValidationError = Validate();
    }

    private string? Validate()
    {
        var slug = Slug.Trim();

        if (string.IsNullOrWhiteSpace(Title))
        {
            return "Title is required.";
        }

        if (!SlugGenerator.IsValid(slug))
        {
            return $"Slug may only contain lowercase latin letters, digits and single hyphens (max {SlugGenerator.MaxLength} characters).";
        }

        if (_isSlugTaken(slug, Id))
        {
            return $"Slug \"{slug}\" is already used by another entry.";
        }

        return null;
    }

    private void NotifyCanSaveChanged()
    {
        OnPropertyChanged(nameof(CanSave));
        CanSaveChanged?.Invoke(this, EventArgs.Empty);
    }
}
