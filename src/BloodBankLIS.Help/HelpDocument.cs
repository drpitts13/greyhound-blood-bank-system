using System.Net;
using System.Text.RegularExpressions;
using Markdig;

namespace BloodBankLIS.Help;

internal static class HelpDocument
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseAutoLinks()
        .Build();

    public static HelpTopic Parse(string guide, string sourceName, string text, Func<string, bool> hasFigure)
    {
        var (fields, lists, body) = SplitFrontMatter(sourceName, text);
        var slug = Required(fields, "slug", sourceName);
        var title = Required(fields, "title", sourceName);
        if (!int.TryParse(Required(fields, "order", sourceName), out var order))
            throw new InvalidOperationException($"Help topic '{sourceName}' has a non-numeric order.");

        var summary = Required(fields, "summary", sourceName);
        var chapter = fields.TryGetValue("chapter", out var chapterValue) && chapterValue.Length > 0
            ? chapterValue
            : "Guide";
        var routes = lists.TryGetValue("routes", out var routeList) ? routeList : [];
        var markdown = body.Trim();
        var html = HelpHtml.Render(markdown, guide, hasFigure);
        var plain = Markdown.ToPlainText(markdown, Pipeline);
        return new HelpTopic(guide, slug, title, order, chapter, summary, routes, markdown, html, plain);
    }

    private static string Required(IReadOnlyDictionary<string, string> fields, string key, string sourceName)
    {
        if (!fields.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Help topic '{sourceName}' is missing '{key}'.");
        return value.Trim();
    }

    internal static (Dictionary<string, string> Fields, Dictionary<string, List<string>> Lists, string Body) SplitFrontMatter(
        string sourceName,
        string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimStart();
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
            throw new InvalidOperationException($"Help topic '{sourceName}' is missing front matter.");

        var end = normalized.IndexOf("\n---\n", StringComparison.Ordinal);
        if (end < 0)
            throw new InvalidOperationException($"Help topic '{sourceName}' does not close its front matter.");

        var yaml = normalized[4..end];
        var body = normalized[(end + 5)..];
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? listKey = null;

        foreach (var rawLine in yaml.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0 || line.TrimStart().StartsWith('#'))
                continue;

            if (line.StartsWith("  - ", StringComparison.Ordinal) || line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (listKey is null)
                    throw new InvalidOperationException($"Help topic '{sourceName}' has a list item without a key.");
                var item = line.Trim()[1..].Trim();
                lists[listKey].Add(Unquote(item));
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
                throw new InvalidOperationException($"Help topic '{sourceName}' has an unreadable front-matter line '{line}'.");

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            listKey = null;
            if (value.Length == 0)
            {
                listKey = key;
                lists[key] = [];
                continue;
            }

            if (value.StartsWith('[') && value.EndsWith(']'))
            {
                lists[key] = value[1..^1]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(Unquote)
                    .ToList();
                continue;
            }

            fields[key] = Unquote(value);
        }

        return (fields, lists, body);
    }

    private static string Unquote(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
            return trimmed[1..^1];
        return trimmed;
    }
}

internal static partial class HelpHtml
{
    public static string Render(string markdown, string guide, Func<string, bool> hasFigure)
    {
        var html = Markdown.ToHtml(markdown, Pipeline);
        html = ImagePattern().Replace(html, match => RewriteImage(match, hasFigure));
        html = LinkPattern().Replace(html, match => RewriteLink(match, guide));
        html = CalloutPattern().Replace(html, match =>
        {
            var label = match.Groups["label"].Value;
            var css = label.StartsWith("Hard stop", StringComparison.OrdinalIgnoreCase) ? "hard"
                : label.StartsWith("Warning", StringComparison.OrdinalIgnoreCase) ? "warn"
                : "policy";
            return $"<blockquote class=\"bb-help-callout {css}\"><p><strong>{label}.</strong>";
        });
        return html;
    }

    private static string RewriteImage(Match match, Func<string, bool> hasFigure)
    {
        var src = WebUtility.HtmlDecode(match.Groups["src"].Value);
        var name = HelpCatalog.FigureFileName(src);
        var alt = match.Groups["alt"].Value;
        if (name is null || !hasFigure(name))
        {
            var caption = string.IsNullOrWhiteSpace(alt) ? name ?? "Figure" : alt;
            return $"<p class=\"muted bb-help-missing-figure\">{caption}</p>";
        }

        return $"<img src=\"/help-figures/{name}\" alt=\"{alt}\" />";
    }

    private static string RewriteLink(Match match, string guide)
    {
        var href = WebUtility.HtmlDecode(match.Groups["href"].Value).Trim();
        if (href.StartsWith('/') ||
            href.StartsWith('#') ||
            href.Contains("://", StringComparison.Ordinal) ||
            href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            return match.Value;

        var slug = href.Split('#', 2)[0];
        if (slug.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            slug = slug[..^3];
        slug = slug.Replace('\\', '/').Trim('/');
        if (slug.Contains('/') || slug.Length == 0)
            return match.Value;

        var hash = href.Contains('#', StringComparison.Ordinal) ? href[href.IndexOf('#')..] : string.Empty;
        return $"<a href=\"/help/{guide}/{slug}{hash}\"";
    }

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseAutoLinks()
        .Build();

    [GeneratedRegex("<img\\s+src=\"(?<src>[^\"]*)\"\\s+alt=\"(?<alt>[^\"]*)\"\\s*/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImagePattern();

    [GeneratedRegex("<a\\s+href=\"(?<href>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinkPattern();

    [GeneratedRegex("<blockquote>\\s*<p>(?:<strong>)?(?<label>Hard stop|Warning|Facility policy)\\.?(?:</strong>)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CalloutPattern();
}
