namespace BloodBankLIS.Help;

public static class ManualPdfExporter
{
    public static void Export(HelpCatalog catalog, string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var guide in HelpGuides.All)
        {
            var bytes = ManualPdfRenderer.Render(catalog, guide);
            var path = Path.Combine(directory, HelpGuides.FileName(guide));
            File.WriteAllBytes(path, bytes);
        }
    }
}

/// <summary>Caches generated manuals for the download buttons.</summary>
public sealed class ManualPdfCache
{
    private readonly HelpCatalog _catalog;
    private readonly Dictionary<string, byte[]> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public ManualPdfCache(HelpCatalog catalog) => _catalog = catalog;

    public byte[] Get(string guide)
    {
        var canonical = HelpGuides.Canonical(guide);
        lock (_gate)
        {
            if (_cache.TryGetValue(canonical, out var cached))
                return cached;

            var rendered = ManualPdfRenderer.Render(_catalog, canonical);
            _cache[canonical] = rendered;
            return rendered;
        }
    }
}
