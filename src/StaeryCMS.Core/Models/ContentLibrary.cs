namespace StaeryCMS.Core.Models;

/// <summary>Everything that is persisted to disk: site settings plus all content entries.</summary>
public sealed class ContentLibrary
{
    public const string DefaultSiteTitle = "My Staery Site";

    public string SiteTitle { get; set; } = DefaultSiteTitle;

    public List<ContentItem> Items { get; set; } = [];
}
