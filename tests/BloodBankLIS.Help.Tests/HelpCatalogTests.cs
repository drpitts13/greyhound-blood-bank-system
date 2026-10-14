using System.Text.RegularExpressions;
using BloodBankLIS.Help;

namespace BloodBankLIS.Help.Tests;

public partial class HelpCatalogTests
{
    private static readonly Lazy<HelpCatalog> Catalog = new(HelpCatalog.LoadEmbedded);

    [Fact]
    public void Catalog_loads_both_guides()
    {
        var guides = Catalog.Value.Topics.Select(topic => topic.Guide).Distinct().OrderBy(guide => guide).ToArray();
        Assert.Equal(new[] { HelpGuides.Admin, HelpGuides.Clinical }, guides);
        Assert.Contains(Catalog.Value.Topics, topic => topic.Slug == "signing-in");
        Assert.Contains(Catalog.Value.Topics, topic => topic.Slug == "facility-policies");
    }

    [Fact]
    public void Patient_chart_tab_matches_the_orders_topic()
    {
        var topic = Catalog.Value.Match("/patients/42?tab=orders");
        Assert.NotNull(topic);
        Assert.Equal("patient-orders", topic.Slug);
    }

    [Fact]
    public void Search_finds_electronic_crossmatch()
    {
        var hits = Catalog.Value.Search("electronic crossmatch");
        Assert.Contains(hits, topic => topic.Slug == "safety" || topic.Slug == "compatibility" || topic.Slug == "facility-policies");
    }

    [Fact]
    public void Every_page_route_has_a_topic()
    {
        var root = RepoRoot();
        var pages = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories);
        var routes = pages
            .SelectMany(file => File.ReadAllLines(file))
            .Select(line => PageRoute().Match(line.Trim()))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.NotEmpty(routes);
        var missing = routes.Where(route => Catalog.Value.Match(Sample(route)) is null).ToList();
        Assert.True(missing.Count == 0, "No help topic for: " + string.Join(", ", missing));
    }

    [Fact]
    public void Both_manuals_render_pdf()
    {
        var clinical = ManualPdfRenderer.Render(Catalog.Value, HelpGuides.Clinical);
        var admin = ManualPdfRenderer.Render(Catalog.Value, HelpGuides.Admin);
        Assert.True(clinical.Length > 1000);
        Assert.True(admin.Length > 1000);
        Assert.Equal((byte)'%', clinical[0]);
        Assert.Equal((byte)'P', clinical[1]);
        Assert.Equal((byte)'D', clinical[2]);
        Assert.Equal((byte)'F', clinical[3]);
        Assert.Equal((byte)'%', admin[0]);
        Assert.True(Catalog.Value.TryGetFigure("figures/signing-in.png", out var clinicalFigure) && clinicalFigure.Length > 1000);
        Assert.True(Catalog.Value.TryGetFigure("figures/admin-home.png", out var adminFigure) && adminFigure.Length > 1000);
    }

    private static string Sample(string template)
    {
        return Regex.Replace(template, @"\{[^}]+\}", match =>
        {
            var name = match.Value.Trim('{', '}').Split(':')[0];
            if (name.Equals("Guide", StringComparison.OrdinalIgnoreCase))
                return "clinical";
            if (name.Equals("Slug", StringComparison.OrdinalIgnoreCase))
                return "signing-in";
            return "1";
        });
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }

    [GeneratedRegex("^@page\\s+\"([^\"]+)\"")]
    private static partial Regex PageRoute();
}
