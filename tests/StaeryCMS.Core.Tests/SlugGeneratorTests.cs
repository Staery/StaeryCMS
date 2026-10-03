using StaeryCMS.Core.Services;

namespace StaeryCMS.Core.Tests;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("  Multiple   spaces -- and dashes  ", "multiple-spaces-and-dashes")]
    [InlineData("C# & .NET 8: What's new?", "c-net-8-what-s-new")]
    [InlineData("Café crème", "cafe-creme")]
    [InlineData("Привет, мир!", "privet-mir")]
    [InlineData("Мой первый пост", "moy-pervyy-post")]
    [InlineData("Щука и ёжик", "shchuka-i-ezhik")]
    public void Generate_ProducesReadableSlugs(string title, string expected) =>
        Assert.Equal(expected, SlugGenerator.Generate(title));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!! ???")]
    public void Generate_FallsBackForTitlesWithoutLetters(string? title) =>
        Assert.Equal(SlugGenerator.Fallback, SlugGenerator.Generate(title));

    [Fact]
    public void Generate_TruncatesLongTitlesWithoutTrailingHyphen()
    {
        var slug = SlugGenerator.Generate(string.Join(' ', Enumerable.Repeat("word", 40)));

        Assert.True(slug.Length <= SlugGenerator.MaxLength);
        Assert.False(slug.EndsWith('-'));
        Assert.True(SlugGenerator.IsValid(slug));
    }

    [Theory]
    [InlineData("hello-world", true)]
    [InlineData("post-2", true)]
    [InlineData("Hello", false)]
    [InlineData("double--hyphen", false)]
    [InlineData("-leading", false)]
    [InlineData("trailing-", false)]
    [InlineData("пост", false)]
    [InlineData("", false)]
    public void IsValid_AcceptsOnlyCanonicalSlugs(string slug, bool expected) =>
        Assert.Equal(expected, SlugGenerator.IsValid(slug));

    [Fact]
    public void MakeUnique_AppendsFirstFreeNumber()
    {
        var taken = new HashSet<string> { "post", "post-2" };

        Assert.Equal("post-3", SlugGenerator.MakeUnique("post", taken));
        Assert.Equal("other", SlugGenerator.MakeUnique("other", taken));
    }
}
