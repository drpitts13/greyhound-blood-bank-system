using System.Text;
using System.Text.RegularExpressions;

namespace BloodBankLIS.Help;

/// <summary>Loads manual topics and resolves the topic for the page the user is on.</summary>
public sealed class HelpCatalog
{
    private readonly List<CompiledRoute> _routes;
    private readonly Dictionary<string, byte[]> _figures;

    private HelpCatalog(IReadOnlyList<HelpTopic> topics, Dictionary<string, byte[]> figures)
    {
        Topics = topics;
        _figures = figures;
        _routes = topics
            .SelectMany(topic => topic.Routes.Select(route => CompiledRoute.Parse(topic, route)))
            .ToList();
    }

    public IReadOnlyList<HelpTopic> Topics { get; }

    public static HelpCatalog LoadEmbedded()
    {
        var assembly = typeof(HelpCatalog).Assembly;
        var markdown = new List<(string Guide, string Name, string Text)>();
        var figures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Missing embedded resource '{resource}'.");
            if (resource.StartsWith("help.figures.", StringComparison.Ordinal) &&
                resource.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                var fileName = resource["help.figures.".Length..];
                figures[fileName] = buffer.ToArray();
                continue;
            }

            if (!resource.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                continue;

            string? guide = null;
            if (resource.StartsWith("help.clinical.", StringComparison.Ordinal))
                guide = HelpGuides.Clinical;
            else if (resource.StartsWith("help.admin.", StringComparison.Ordinal))
                guide = HelpGuides.Admin;

            if (guide is null)
                continue;

            using var reader = new StreamReader(stream, Encoding.UTF8);
            markdown.Add((guide, resource, reader.ReadToEnd()));
        }

        return FromDocuments(markdown, figures);
    }

    public static HelpCatalog LoadFromDirectory(string root)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Help content directory '{root}' was not found.");

        var markdown = new List<(string Guide, string Name, string Text)>();
        foreach (var guide in HelpGuides.All)
        {
            var dir = Path.Combine(root, guide);
            if (!Directory.Exists(dir))
                continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.md"))
            {
                markdown.Add((guide, Path.GetFileName(file), File.ReadAllText(file)));
            }
        }

        var figures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var figureDir = Path.Combine(root, "figures");
        if (Directory.Exists(figureDir))
        {
            foreach (var file in Directory.EnumerateFiles(figureDir, "*.png"))
                figures[Path.GetFileName(file)] = File.ReadAllBytes(file);
        }

        return FromDocuments(markdown, figures);
    }

    public IEnumerable<HelpTopic> InGuide(string guide)
    {
        var canonical = HelpGuides.Canonical(guide);
        return Topics.Where(topic => topic.Guide == canonical).OrderBy(topic => topic.Order);
    }

    public HelpTopic? Find(string guide, string slug)
    {
        if (!HelpGuides.IsKnown(guide) || string.IsNullOrWhiteSpace(slug))
            return null;

        var canonical = HelpGuides.Canonical(guide);
        return Topics.FirstOrDefault(topic =>
            topic.Guide == canonical &&
            topic.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
    }

    public HelpTopic? Match(string? relativeUri)
    {
        var (segments, query) = Split(relativeUri);
        CompiledRoute? best = null;
        foreach (var route in _routes)
        {
            if (!route.Matches(segments, query))
                continue;
            if (best is null || route.Specificity > best.Specificity ||
                (route.Specificity == best.Specificity &&
                 (route.Topic.Order < best.Topic.Order ||
                  (route.Topic.Order == best.Topic.Order &&
                   string.Compare(route.Topic.Slug, best.Topic.Slug, StringComparison.Ordinal) < 0))))
            {
                best = route;
            }
        }

        return best?.Topic;
    }

    public IReadOnlyList<HelpTopic> Search(string? query, string? guide = null)
    {
        IEnumerable<HelpTopic> source = string.IsNullOrWhiteSpace(guide)
            ? Topics
            : InGuide(guide);

        var ordered = source.OrderBy(topic => topic.Guide, StringComparer.Ordinal)
            .ThenBy(topic => topic.Order)
            .ToList();

        if (string.IsNullOrWhiteSpace(query))
            return ordered;

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ordered
            .Select(topic => (topic, score: Score(topic, terms)))
            .Where(hit => hit.score > 0)
            .OrderByDescending(hit => hit.score)
            .ThenBy(hit => hit.topic.Guide, StringComparer.Ordinal)
            .ThenBy(hit => hit.topic.Order)
            .Select(hit => hit.topic)
            .ToList();
    }

    public bool TryGetFigure(string reference, out byte[] bytes)
    {
        var name = FigureFileName(reference);
        if (name is null)
        {
            bytes = [];
            return false;
        }

        return _figures.TryGetValue(name, out bytes!);
    }

    public static string? FigureFileName(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        var path = reference.Split('?', 2)[0].Replace('\\', '/').Trim();
        var name = Path.GetFileName(path);
        if (name.Length == 0 || name.Contains("..", StringComparison.Ordinal))
            return null;
        if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            return null;
        if (!FigureNamePattern.IsMatch(name))
            return null;
        return name;
    }

    private static int Score(HelpTopic topic, string[] terms)
    {
        var score = 0;
        foreach (var term in terms)
        {
            if (topic.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += 5;
            else if (topic.Summary.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += 3;
            else if (topic.PlainText.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += 1;
            else
                return 0;
        }

        return score;
    }

    internal static (string[] Segments, Dictionary<string, string> Query) Split(string? relativeUri)
    {
        var value = (relativeUri ?? string.Empty).Trim();
        if (value.StartsWith('/'))
            value = value[1..];

        var hash = value.IndexOf('#');
        if (hash >= 0)
            value = value[..hash];

        var qIndex = value.IndexOf('?');
        var path = qIndex >= 0 ? value[..qIndex] : value;
        var queryText = qIndex >= 0 ? value[(qIndex + 1)..] : string.Empty;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (queryText.Length > 0)
        {
            foreach (var pair in queryText.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var eq = pair.IndexOf('=');
                var key = Uri.UnescapeDataString(eq >= 0 ? pair[..eq] : pair);
                var raw = eq >= 0 ? pair[(eq + 1)..] : string.Empty;
                if (key.Length == 0)
                    continue;
                query[key] = Uri.UnescapeDataString(raw);
            }
        }

        return (segments, query);
    }

    private static HelpCatalog FromDocuments(
        IReadOnlyList<(string Guide, string Name, string Text)> documents,
        Dictionary<string, byte[]> figures)
    {
        var topics = new List<HelpTopic>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (guide, name, text) in documents)
        {
            var topic = HelpDocument.Parse(guide, name, text, figure =>
                figures.ContainsKey(FigureFileName(figure) ?? "\0"));
            var key = guide + "/" + topic.Slug;
            if (!seen.Add(key))
                throw new InvalidOperationException($"Duplicate help topic '{key}' (from {name}).");
            topics.Add(topic);
        }

        if (topics.Count == 0)
            throw new InvalidOperationException("The help catalog does not contain any topics.");

        return new HelpCatalog(topics, figures);
    }

    private static readonly Regex FigureNamePattern = new("^[A-Za-z0-9][A-Za-z0-9._-]*\\.png$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private sealed class CompiledRoute
    {
        private CompiledRoute(HelpTopic topic, string[] segments, Dictionary<string, string> query, int specificity)
        {
            Topic = topic;
            Segments = segments;
            Query = query;
            Specificity = specificity;
        }

        public HelpTopic Topic { get; }
        public string[] Segments { get; }
        public Dictionary<string, string> Query { get; }
        public int Specificity { get; }

        public static CompiledRoute Parse(HelpTopic topic, string route)
        {
            var (segments, query) = Split(route);
            var literals = segments.Count(segment => !IsParameter(segment));
            var specificity = query.Count * 100 + literals * 10 + segments.Length;
            return new CompiledRoute(topic, segments, query, specificity);
        }

        public bool Matches(string[] segments, IReadOnlyDictionary<string, string> query)
        {
            if (segments.Length != Segments.Length)
                return false;

            for (var i = 0; i < Segments.Length; i++)
            {
                if (IsParameter(Segments[i]))
                {
                    if (segments[i].Length == 0)
                        return false;
                    continue;
                }

                if (!Segments[i].Equals(segments[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            foreach (var (key, expected) in Query)
            {
                if (!query.TryGetValue(key, out var actual) ||
                    !expected.Equals(actual, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private static bool IsParameter(string segment) =>
            segment.Length >= 2 && segment[0] == '{' && segment[^1] == '}';
    }
}
