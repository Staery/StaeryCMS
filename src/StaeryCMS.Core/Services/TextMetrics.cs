namespace StaeryCMS.Core.Services;

/// <summary>Word count and reading-time estimates for content bodies.</summary>
public static class TextMetrics
{
    /// <summary>Average adult silent reading speed used for estimates.</summary>
    public const int WordsPerMinute = 200;

    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var count = 0;
        var inWord = false;

        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (!inWord)
                {
                    count++;
                    inWord = true;
                }
            }
            else if (ch is not ('\'' or '’' or '-'))
            {
                inWord = false;
            }
        }

        return count;
    }

    /// <summary>Estimated reading time in whole minutes; at least one minute for any non-empty text.</summary>
    public static int EstimateReadingMinutes(int wordCount) =>
        wordCount <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(wordCount / (double)WordsPerMinute));
}
