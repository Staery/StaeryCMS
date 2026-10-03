namespace StaeryCMS.Core.Models;

/// <summary>Aggregated numbers shown on the dashboard.</summary>
public sealed record ContentStatistics(int Total, int Published, int Drafts, int Archived, int TotalWords)
{
    public static ContentStatistics Empty { get; } = new(0, 0, 0, 0, 0);

    public static ContentStatistics From(IEnumerable<ContentItem> items)
    {
        int total = 0, published = 0, drafts = 0, archived = 0, words = 0;

        foreach (var item in items)
        {
            total++;
            words += Services.TextMetrics.CountWords(item.Body);

            switch (item.Status)
            {
                case ContentStatus.Published: published++; break;
                case ContentStatus.Draft: drafts++; break;
                case ContentStatus.Archived: archived++; break;
            }
        }

        return new ContentStatistics(total, published, drafts, archived, words);
    }
}
