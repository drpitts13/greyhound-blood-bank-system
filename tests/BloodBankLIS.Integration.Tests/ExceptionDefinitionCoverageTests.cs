using System.Reflection;
using System.Text.RegularExpressions;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-389: every override the app checks against the exception catalog has an active, overridable definition
// seeded, so a new override path cannot ship to be refused at the bench with EXC-DEF-MISSING.
public class ExceptionDefinitionCoverageTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ExceptionDefinitionCoverageTests(SqliteContextFactory factory) => _factory = factory;

    private static readonly Regex AccessCall = new(
        @"ExceptionOverridePolicy\.EvaluateAccess\(\s*[^,]+,\s*[^,]+,\s*(?<code>[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*)\s*\)",
        RegexOptions.Singleline);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root.");
    }

    /// <summary>The rule codes passed to every EvaluateAccess call in src, resolved from their Domain constants.</summary>
    private static IReadOnlyDictionary<string, string> CheckedCodes()
    {
        var domain = typeof(ExceptionOverridePolicy).Assembly;
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            foreach (Match match in AccessCall.Matches(File.ReadAllText(file)))
            {
                var expression = match.Groups["code"].Value;
                var parts = expression.Split('.');
                var type = domain.GetTypes().SingleOrDefault(t => t.Name == parts[0])
                    ?? throw new InvalidOperationException($"{Path.GetFileName(file)}: cannot resolve type in {expression}.");
                var field = type.GetField(parts[1], BindingFlags.Public | BindingFlags.Static)
                    ?? throw new InvalidOperationException($"{Path.GetFileName(file)}: cannot resolve {expression}.");
                found[expression] = (string)field.GetValue(null)!;
            }
        }

        return found;
    }

    [Fact]
    public void TheScan_FindsTheKnownOverridePaths()
    {
        var codes = CheckedCodes().Values.ToHashSet();
        Assert.Contains(BloodAttributeCompatibilityRule.AntigenNegCode, codes);
        Assert.Contains(SpecimenExpirationOverrideRule.Code, codes);
        Assert.Contains(CrossmatchSpecimenRule.RetrospectiveCode, codes);
    }

    [Fact]
    public async Task EveryCheckedCode_HasAnActiveOverridableSeededDefinition()
    {
        await using (var c = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(c);
        }

        await using var verify = _factory.Create();
        var definitions = await verify.ExceptionDefinitions.AsNoTracking().ToListAsync();
        foreach (var (expression, code) in CheckedCodes())
        {
            var definition = definitions.SingleOrDefault(d => d.RuleCode == code);
            Assert.True(definition is not null, $"{expression} ({code}) is checked by an override but has no seeded exception definition.");
            Assert.True(definition!.IsActive, $"{code} is seeded inactive.");
            Assert.True(definition.IsOverridable, $"{code} is checked as an override but seeded as not overridable.");
        }
    }
}
