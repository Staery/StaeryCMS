using Markdig;

namespace StaeryCMS.Core.Services;

/// <summary>Converts Markdown bodies to HTML.</summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    /// <remarks>Raw HTML in the source is escaped, so exported pages cannot carry injected scripts.</remarks>
    public static string ToHtml(string? markdown) => Markdown.ToHtml(markdown ?? string.Empty, Pipeline);
}
