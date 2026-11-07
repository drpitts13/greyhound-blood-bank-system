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
