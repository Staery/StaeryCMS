using CommunityToolkit.Mvvm.ComponentModel;
using StaeryCMS.Core.Models;

namespace StaeryCMS.Core.ViewModels;

/// <summary>Sidebar navigation entry: a status filter together with the number of matching entries.</summary>
public sealed partial class StatusFilterOption(string label, string iconKey, ContentStatus? status) : ObservableObject
{
    public string Label { get; } = label;

    /// <summary>Resource key of the icon geometry shown next to the label.</summary>
    public string IconKey { get; } = iconKey;

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
