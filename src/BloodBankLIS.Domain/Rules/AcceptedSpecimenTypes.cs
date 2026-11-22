namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The specimen types a test accepts, stored in <c>TestDefinition.RequiredSpecimenType</c> as an ordered list
/// ("EDTA, CLOT"): the first is preferred and prefills accession; any listed type may be resulted. Empty means any type.
/// A single code (the original form) is a list of one.
/// </summary>
public static class AcceptedSpecimenTypes
{
    public static IReadOnlyList<string> Parse(string? stored) =>
        (stored ?? string.Empty)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>The list as stored (preferred first), or null for any type.</summary>
    public static string? Normalize(string? stored)
    {
        var codes = Parse(stored);
        return codes.Count == 0 ? null : string.Join(", ", codes);
    }

    public static string? Preferred(string? stored) => Parse(stored).FirstOrDefault();

    public static bool Accepts(string? stored, string? specimenType)
    {
        var codes = Parse(stored);
        return codes.Count == 0
            || (!string.IsNullOrWhiteSpace(specimenType) && codes.Contains(specimenType.Trim().ToUpperInvariant()));
    }

    /// <summary>
    /// The specimen types every one of the tests accepts, in the order of the first restricted test (its preferred type
    /// first). Restricted is false when no test limits the type (any tube suits them all).
    /// </summary>
    public static (IReadOnlyList<string> Types, bool Restricted) Common(IEnumerable<(string TestCode, string? Stored)> tests)
    {
        IReadOnlyList<string>? common = null;
        foreach (var (_, stored) in tests)
        {
            var accepted = Parse(stored);
            if (accepted.Count == 0)
            {
                continue;
            }

            common = common is null ? accepted : common.Where(accepted.Contains).ToList();
        }

        return common is null ? ([], false) : (common, true);
    }

    /// <summary>
    /// For order entry: which types the selected tests accept, and whether one tube can serve them all. Null when no
    /// selected test limits the type.
    /// </summary>
    public static string? OrderNote(IEnumerable<(string TestCode, string? Stored)> tests)
    {
        var list = tests.ToList();
        var restricted = list.Where(t => Parse(t.Stored).Count > 0).ToList();
        if (restricted.Count == 0)
        {
            return null;
        }

        var accepts = string.Join("; ", restricted.Select(t => $"{t.TestCode.Trim().ToUpperInvariant()} accepts {Describe(t.Stored)}"));
        var (common, _) = Common(list);
        return common.Count switch
        {
            0 => $"{accepts}. No one specimen type suits all of these tests: collect a tube for each.",
            _ when restricted.Count == 1 => $"{accepts}.",
            _ => $"{accepts}. {Describe(string.Join(",", common))} suits them all."
        };
    }

    /// <summary>"EDTA" or "EDTA or CLOT" or "EDTA, CLOT, or HEP", for messages.</summary>
    public static string Describe(string? stored)
    {
        var codes = Parse(stored);
        return codes.Count switch
        {
            0 => "any type",
            1 => codes[0],
            2 => $"{codes[0]} or {codes[1]}",
            _ => $"{string.Join(", ", codes.Take(codes.Count - 1))}, or {codes[^1]}"
        };
    }
}
