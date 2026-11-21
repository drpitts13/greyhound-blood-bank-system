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

    /// <summary>
    /// The visit a new tube starts on in the accession form: the patient's open visit when there is exactly one; with
    /// none or several open, nothing is chosen for the user.
    /// </summary>
    public static long? DefaultForNewTube(IEnumerable<EncounterDto> visits)
    {
        var (visit, otherOpen) = For(visits);
        return visit is not null && otherOpen == 0 ? visit.Id : null;
    }

    /// <summary>Visits a tube can be put on (not cancelled), open ones first, newest admitted first.</summary>
    public static IReadOnlyList<EncounterDto> ChoicesForNewTube(IEnumerable<EncounterDto> visits) =>
        visits.Where(v => v.Status != EncounterStatus.Cancelled)
            .OrderBy(v => v.Status == EncounterStatus.Active && v.DischargeUtc is null ? 0 : 1)
            .ThenByDescending(v => v.AdmitUtc ?? DateTime.MinValue)
            .ThenByDescending(v => v.Id)
            .ToList();

    /// <summary>"VIS-1 · Inpatient · ICU 4 · open" or "... · discharged 2026-10-02": a visit as a choice.</summary>
    public static string ChoiceLabel(EncounterDto visit)
    {
        var state = visit.Status == EncounterStatus.Active && visit.DischargeUtc is null
            ? "open"
            : visit.DischargeUtc is DateTime discharged ? $"discharged {discharged:yyyy-MM-dd}" : visit.Status.ToString().ToLowerInvariant();
        return string.Join(" · ", new[]
        {
            visit.VisitNumber,
            visit.EncounterType == EncounterType.Unknown ? null : visit.EncounterType.ToString(),
            (visit.CurrentLocation ?? visit.AdmissionLocation)?.Trim() is { Length: > 0 } place ? place : null,
            state
        }.Where(p => p is not null));
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
