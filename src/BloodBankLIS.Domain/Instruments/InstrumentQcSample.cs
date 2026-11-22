namespace BloodBankLIS.Domain.Instruments;

/// <summary>
/// Recognises an instrument's own QC samples by their sample ID prefix (end-user testing pass 84: a daily QC run on the
/// analyzer landed in the failures queue as "No specimen matched" every morning). The prefixes are the facility's.
/// </summary>
public static class InstrumentQcSample
{
    public const int MaxLength = 200;

    /// <summary>Prefixes trimmed and upper-cased, without duplicates; separated by commas, semicolons or spaces.</summary>
    public static IReadOnlyList<string> Prefixes(string? prefixes) =>
        (prefixes ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToUpperInvariant())
            .Distinct()
            .ToList();

    /// <summary>The prefixes as stored: normalised and comma-separated, or null when none.</summary>
    public static string? Normalize(string? prefixes) =>
        Prefixes(prefixes) is { Count: > 0 } list ? string.Join(", ", list) : null;

    public static bool IsQc(string? sampleId, string? prefixes) =>
        !string.IsNullOrWhiteSpace(sampleId)
        && Prefixes(prefixes).Any(p => sampleId.Trim().StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
