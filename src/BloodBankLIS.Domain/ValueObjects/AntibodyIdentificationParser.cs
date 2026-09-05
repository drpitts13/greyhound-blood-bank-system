using System.Text.RegularExpressions;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>Catalog row used to resolve an antibody-identification result token.</summary>
public sealed record AntibodyCatalogItem(long Id, string Code, string Name, string AntibodyName);

/// <summary>One token from an ABID result and the catalog row it resolved to, if any.</summary>
public sealed record AntibodyIdentificationHit(string Token, AntibodyCatalogItem? CatalogItem);

/// <summary>
/// Parses free-text / coded antibody-identification values (anti-K, anti-E + anti-c)
/// against the blood-attribute catalog so verified ABID results can post history
/// the way SoftBank and SafeTrace do.
/// </summary>
public static class AntibodyIdentificationParser
{
    public const string UnmatchedRuleCode = "RES-ABID-UNMATCHED";

    private static readonly Regex SplitPattern = new(
        @"[,;+/|]|\s+and\s+|\s+with\s+|\s+&\s+|\r?\n",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> NegativePhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "negative",
        "neg",
        "none",
        "none identified",
        "none detected",
        "no antibody",
        "no antibodies",
        "not identified",
        "unable to identify",
        "unidentified",
        "pending",
        "inconclusive",
        "no atypical antibodies"
    };

    public static bool IsNegativeOrUnidentified(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = CollapseWhitespace(value);
        return NegativePhrases.Contains(trimmed);
    }

    public static IReadOnlyList<string> SplitTokens(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return SplitPattern
            .Split(value)
            .Select(NormalizeToken)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<AntibodyIdentificationHit> Resolve(
        string? value,
        IReadOnlyList<AntibodyCatalogItem> catalog)
    {
        if (IsNegativeOrUnidentified(value) || catalog.Count == 0)
        {
            return [];
        }

        var hits = new List<AntibodyIdentificationHit>();
        var seenIds = new HashSet<long>();

        foreach (var token in SplitTokens(value))
        {
            var match = MatchCatalog(token, catalog);
            if (match is not null && !seenIds.Add(match.Id))
            {
                continue;
            }

            hits.Add(new AntibodyIdentificationHit(token, match));
        }

        return hits;
    }

    public static string Format(IEnumerable<string> labels)
    {
        var parts = labels
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(NormalizeToken)
            .Where(l => l.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return string.Join(", ", parts);
    }

    public static bool LooksLikeAntibodyToken(string token)
    {
        var normalized = NormalizeToken(token);
        return normalized.StartsWith("anti-", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("anti", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Labels that a verified ABID result would post: catalog antibody names,
    /// plus unmatched anti-* tokens. Used to compare free-text verify against a workup.
    /// </summary>
    public static IReadOnlyList<string> PostedLabels(IReadOnlyList<AntibodyIdentificationHit> hits)
    {
        if (hits.Count == 0)
        {
            return [];
        }

        return hits
            .Select(h => h.CatalogItem?.AntibodyName
                         ?? (LooksLikeAntibodyToken(h.Token) ? h.Token : null))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Fallback when the catalog is empty: anti-* tokens that would post as free-text.
    /// </summary>
    public static IReadOnlyList<string> AntibodyLikeTokens(string? value) =>
        SplitTokens(value).Where(LooksLikeAntibodyToken).ToList();

    private static AntibodyCatalogItem? MatchCatalog(string token, IReadOnlyList<AntibodyCatalogItem> catalog)
    {
        var stripped = StripAntiPrefix(token);

        var ordinalAntibody = catalog.FirstOrDefault(c =>
            string.Equals(c.AntibodyName, token, StringComparison.Ordinal));
        if (ordinalAntibody is not null)
        {
            return ordinalAntibody;
        }

        var ordinalCode = catalog.FirstOrDefault(c =>
            string.Equals(c.Code, token, StringComparison.Ordinal)
            || string.Equals(c.Code, stripped, StringComparison.Ordinal));
        if (ordinalCode is not null)
        {
            return ordinalCode;
        }

        return UniqueIgnoreCase(catalog, c => c.AntibodyName, token)
               ?? UniqueIgnoreCase(catalog, c => c.Code, token)
               ?? UniqueIgnoreCase(catalog, c => c.Code, stripped)
               ?? UniqueIgnoreCase(catalog, c => c.Name, token)
               ?? UniqueIgnoreCase(catalog, c => c.Name, stripped);
    }

    private static AntibodyCatalogItem? UniqueIgnoreCase(
        IReadOnlyList<AntibodyCatalogItem> catalog,
        Func<AntibodyCatalogItem, string> selector,
        string token)
    {
        var matches = catalog
            .Where(c => string.Equals(selector(c), token, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static string NormalizeToken(string token)
    {
        var trimmed = CollapseWhitespace(token);
        if (trimmed.StartsWith("anti ", StringComparison.OrdinalIgnoreCase))
        {
            return "anti-" + trimmed[5..];
        }

        return trimmed;
    }

    private static string StripAntiPrefix(string token)
    {
        if (token.StartsWith("anti-", StringComparison.OrdinalIgnoreCase))
        {
            return token[5..].Trim();
        }

        if (token.StartsWith("anti", StringComparison.OrdinalIgnoreCase) && token.Length > 4)
        {
            return token[4..].Trim();
        }

        return token;
    }

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
