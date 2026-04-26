using Markdig;
using Microsoft.AspNetCore.Components;

namespace Yaaw.Web.Services;

public sealed class MarkdownService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public MarkupString Render(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new MarkupString("");
        }

        string html = Markdown.ToHtml(markdown, Pipeline);
        return new MarkupString(html);
    }
}
