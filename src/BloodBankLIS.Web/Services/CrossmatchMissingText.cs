using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// What the Compatibility crossmatch form says while Record crossmatch is not yet available (end-user testing pass 287).
/// Before, the parts were joined with commas alone ("Choose a specimen (...), a unit first.") and the specimen part said
/// to accession one "if none is listed" even when the patient's only specimens were listed and expired.
/// </summary>
public static class CrossmatchMissingText
{
    public static string? For(bool patientChosen, bool specimenChosen, bool unitChosen, IReadOnlyList<SpecimenDto> specimens, DateTime nowUtc)
    {
        var missing = new List<string>();
        if (!patientChosen)
        {
            missing.Add("a patient");
        }

        if (!specimenChosen)
        {
            missing.Add(!patientChosen ? "a specimen" : SpecimenPart(specimens, nowUtc));
        }

        if (!unitChosen)
        {
            missing.Add("a unit");
        }

        return missing.Count == 0 ? null : $"Choose {Join(missing)} first.";
    }

    /// <summary>True when the chosen patient has no accepted, unexpired specimen to crossmatch on.</summary>
    public static bool NoCurrentSpecimen(IReadOnlyList<SpecimenDto> specimens, DateTime nowUtc) =>
        !specimens.Any(s => IsCurrent(s, nowUtc));

    private static string SpecimenPart(IReadOnlyList<SpecimenDto> specimens, DateTime nowUtc)
    {
        if (specimens.Count == 0)
        {
            return "a specimen (this patient has none: collect one and accession it on the chart)";
        }

        if (specimens.Any(s => IsCurrent(s, nowUtc)))
        {
            return "a specimen";
        }

        var latestExpired = specimens
            .Where(s => s.ExpiresUtc is DateTime e && e <= nowUtc)
            .OrderByDescending(s => s.ExpiresUtc)
            .FirstOrDefault();
        return latestExpired is null
            ? "a specimen (this patient has no accepted specimen: collect a new one and accession it on the chart)"
            : $"a specimen (this patient has no current one: the latest, {latestExpired.AccessionNumber}, expired {ClinicalTimeText.Local(latestExpired.ExpiresUtc!.Value)}; collect a new one and accession it on the chart)";
    }

    private static bool IsCurrent(SpecimenDto s, DateTime nowUtc) =>
        s.Status == SpecimenStatus.Accepted && (s.ExpiresUtc is null || s.ExpiresUtc > nowUtc);

    private static string Join(IReadOnlyList<string> parts) => parts.Count switch
    {
        1 => parts[0],
        2 => $"{parts[0]} and {parts[1]}",
        _ => string.Join(", ", parts.Take(parts.Count - 1)) + $" and {parts[^1]}"
    };
}
