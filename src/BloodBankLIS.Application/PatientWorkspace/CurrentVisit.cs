using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>
/// The visit the chart header shows as current: the most recently admitted open visit (active and not discharged),
/// and how many other visits are open, so a second open visit is never hidden.
/// </summary>
public static class CurrentVisit
{
    public static (EncounterDto? Visit, int OtherOpen) For(IEnumerable<EncounterDto> visits) =>
        Pick(visits, v => v.Status, v => v.DischargeUtc, v => v.AdmitUtc, v => v.Id);

    /// <summary>The same choice from stored visits (patient list, top-bar lookup).</summary>
    public static (Encounter? Visit, int OtherOpen) For(IEnumerable<Encounter> visits) =>
        Pick(visits, v => v.Status, v => v.DischargeUtc, v => v.AdmitUtc, v => v.Id);

    private static (T? Visit, int OtherOpen) Pick<T>(
        IEnumerable<T> visits, Func<T, EncounterStatus> status, Func<T, DateTime?> discharged, Func<T, DateTime?> admitted, Func<T, long> id)
        where T : class
    {
        var open = visits
            .Where(v => status(v) == EncounterStatus.Active && discharged(v) is null)
            .OrderByDescending(v => admitted(v) ?? DateTime.MinValue)
            .ThenByDescending(id)
            .ToList();
        return (open.FirstOrDefault(), Math.Max(0, open.Count - 1));
    }

    /// <summary>"Inpatient · ICU 4": where the patient is, without the attending (lists and lookup rows).</summary>
    public static string Where(Encounter visit) =>
        string.Join(" · ", new[]
        {
            visit.EncounterType == EncounterType.Unknown ? null : visit.EncounterType.ToString(),
            (visit.CurrentLocation ?? visit.AdmissionLocation)?.Trim() is { Length: > 0 } place ? place : null
        }.Where(p => p is not null));

    /// <summary>"Inpatient · ICU 4 · Attending Dr. Smith": the parts that are recorded, in that order.</summary>
    public static string Describe(EncounterDto visit) =>
        string.Join(" · ", new[]
        {
            visit.EncounterType == EncounterType.Unknown ? null : visit.EncounterType.ToString(),
            (visit.CurrentLocation ?? visit.AdmissionLocation)?.Trim() is { Length: > 0 } place ? place : null,
            string.IsNullOrWhiteSpace(visit.AttendingProvider) ? null : $"Attending {visit.AttendingProvider.Trim()}"
        }.Where(p => p is not null));
}
