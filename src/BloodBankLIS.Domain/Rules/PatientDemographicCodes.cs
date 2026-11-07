namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Local race and ethnicity codes stored on the patient, following the US OMB minimum categories. Interfaces translate
/// their own codes (for example HL7 table 0005 / CDC race codes) to these.
/// </summary>
public static class PatientDemographicCodes
{
    public static IReadOnlyList<(string Code, string Label)> Races { get; } =
    [
        ("AIAN", "American Indian or Alaska Native"),
        ("ASIAN", "Asian"),
        ("BLACK", "Black or African American"),
        ("NHPI", "Native Hawaiian or Other Pacific Islander"),
        ("WHITE", "White"),
        ("OTHER", "Other race"),
        ("UNKNOWN", "Unknown"),
        ("DECLINED", "Declined to answer")
    ];

    public static IReadOnlyList<(string Code, string Label)> Ethnicities { get; } =
    [
        ("HISPANIC", "Hispanic or Latino"),
        ("NOT-HISPANIC", "Not Hispanic or Latino"),
        ("UNKNOWN", "Unknown"),
        ("DECLINED", "Declined to answer")
    ];

    public static bool IsRace(string? code) => Races.Any(r => string.Equals(r.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsEthnicity(string? code) => Ethnicities.Any(e => string.Equals(e.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string? RaceLabel(string? code) => Races.FirstOrDefault(r => string.Equals(r.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase)).Label;

    public static string? EthnicityLabel(string? code) => Ethnicities.FirstOrDefault(e => string.Equals(e.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase)).Label;

    /// <summary>The stored (upper-case) code for a local value, or null when it is not one of the list.</summary>
    public static string? NormalizeRace(string? code) => IsRace(code) ? code!.Trim().ToUpperInvariant() : null;

    public static string? NormalizeEthnicity(string? code) => IsEthnicity(code) ? code!.Trim().ToUpperInvariant() : null;
}
