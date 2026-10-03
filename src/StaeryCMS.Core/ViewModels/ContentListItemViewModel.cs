using CommunityToolkit.Mvvm.ComponentModel;
using StaeryCMS.Core.Models;
using StaeryCMS.Core.Services;

namespace StaeryCMS.Core.ViewModels;

/// <summary>Read-only row in the content list.</summary>
public sealed class ContentListItemViewModel(ContentItem model) : ObservableObject
{
    public ContentItem Model { get; } = model;

    public Guid Id => Model.Id;

    public string Title => string.IsNullOrWhiteSpace(Model.Title) ? "Untitled" : Model.Title;

    public string Summary => Model.Summary;

    public string Category => Model.Category;

    public ContentStatus Status => Model.Status;

    public DateTimeOffset UpdatedAt => Model.UpdatedAt;

    public string TagsText => string.Join("  ", Model.Tags.Select(tag => "#" + tag));

    public int WordCount => TextMetrics.CountWords(Model.Body);

    public bool Matches(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        var term = searchText.Trim();
        return Contains(Model.Title) || Contains(Model.Summary) || Contains(Model.Category) || Contains(Model.Body) ||
               Model.Tags.Any(Contains);

        bool Contains(string value) => value.Contains(term, StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>Notifies the view that the underlying entry has changed.</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);
}
