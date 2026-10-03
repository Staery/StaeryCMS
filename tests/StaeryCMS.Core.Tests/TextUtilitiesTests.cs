using StaeryCMS.Core.Services;

namespace StaeryCMS.Core.Tests;

public class TextUtilitiesTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("   \n\t ", 0)]
    [InlineData("one", 1)]
    [InlineData("two words", 2)]
    [InlineData("# Heading\n\n- item one\n- item two", 5)]
    [InlineData("don't stop well-known", 3)]
    [InlineData("Привет, мир! 2025", 3)]
    public void CountWords_CountsLetterAndDigitRuns(string? text, int expected) =>
        Assert.Equal(expected, TextMetrics.CountWords(text));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(200, 1)]
    [InlineData(201, 2)]
    [InlineData(1000, 5)]
    public void EstimateReadingMinutes_RoundsUp(int words, int expected) =>
        Assert.Equal(expected, TextMetrics.EstimateReadingMinutes(words));

    [Fact]
    public void TagParser_TrimsDeduplicatesAndStripsHashes()
    {
        var tags = TagParser.Parse(" #dotnet, wpf;; DotNet , ,mvvm ");

        Assert.Equal(["dotnet", "wpf", "mvvm"], tags);
    }

    [Fact]
    public void TagParser_FormatRoundTrips()
    {
        var tags = new[] { "a", "b c" };

        Assert.Equal(tags, TagParser.Parse(TagParser.Format(tags)));
    }
}
