namespace StaeryCMS.Core.Services;

/// <summary>Converts between the comma-separated tag text shown in the editor and a tag list.</summary>
public static class TagParser
{
    private static readonly char[] Separators = [',', ';'];

    public static List<string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tags = new List<string>();

        foreach (var raw in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tag = raw.TrimStart('#');
            if (tag.Length > 0 && seen.Add(tag))
            {
                tags.Add(tag);
            }
        }

        return tags;
    }

    public static string Format(IEnumerable<string> tags) => string.Join(", ", tags);
}
