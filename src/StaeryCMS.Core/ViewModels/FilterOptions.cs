using CommunityToolkit.Mvvm.ComponentModel;
using StaeryCMS.Core.Models;

namespace StaeryCMS.Core.ViewModels;

/// <summary>Sidebar navigation entry: a status filter together with the number of matching entries.</summary>
public sealed partial class StatusFilterOption(string label, string glyph, ContentStatus? status) : ObservableObject
{
    public string Label { get; } = label;

    /// <summary>Segoe Fluent Icons / Segoe MDL2 Assets glyph.</summary>
    public string Glyph { get; } = glyph;

    /// <summary>The status to show, or <see langword="null"/> for every entry.</summary>
    public ContentStatus? Status { get; } = status;

    [ObservableProperty]
    private int _count;

    public bool Includes(ContentStatus status) => Status is null || Status == status;
}

public enum ContentSortOrder
{
    RecentlyUpdated,
    RecentlyCreated,
    Title,
}

public sealed record SortOption(string Label, ContentSortOrder Order);
