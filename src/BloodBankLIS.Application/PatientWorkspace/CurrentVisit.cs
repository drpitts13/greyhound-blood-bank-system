using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>
/// The visit the chart header shows as current: the most recently admitted open visit (active and not discharged),
/// and how many other visits are open, so a second open visit is never hidden.
/// </summary>
public static class CurrentVisit
{
    public static (EncounterDto? Visit, int OtherOpen) For(IEnumerable<EncounterDto> visits)
    {
        var open = visits
            .Where(v => v.Status == EncounterStatus.Active && v.DischargeUtc is null)
            .OrderByDescending(v => v.AdmitUtc ?? DateTime.MinValue)
            .ThenByDescending(v => v.Id)
            .ToList();
        return (open.FirstOrDefault(), Math.Max(0, open.Count - 1));
    }

    /// <summary>"Inpatient · ICU 4 · Attending Dr. Smith": the parts that are recorded, in that order.</summary>
    public static string Describe(EncounterDto visit) =>
        string.Join(" · ", new[]
        {
            visit.EncounterType == EncounterType.Unknown ? null : visit.EncounterType.ToString(),
            (visit.CurrentLocation ?? visit.AdmissionLocation)?.Trim() is { Length: > 0 } place ? place : null,
            string.IsNullOrWhiteSpace(visit.AttendingProvider) ? null : $"Attending {visit.AttendingProvider.Trim()}"
        }.Where(p => p is not null));
}
