using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Issuing;

/// <summary>
/// Where the patient is, as their visit records it, to suggest as the issue destination: the reservation's own visit
/// when it is still open, otherwise the patient's most recent open visit with a location. A discharged or cancelled
/// visit suggests nothing. It is only a suggestion; the issuer confirms it, because a visit location can lag behind a
/// patient who has gone to theatre or imaging.
/// </summary>
public static class PatientVisitLocation
{
    public static string? For(long? reservationEncounterId, IEnumerable<Encounter> patientEncounters) =>
        For(reservationEncounterId, patientEncounters.Select(e => (e.Id, e.Status, e.AdmitUtc, e.DischargeUtc, e.CurrentLocation)));

    /// <summary>The same choice from the chart's visit list.</summary>
    public static string? For(long? reservationEncounterId, IEnumerable<EncounterDto> patientEncounters) =>
        For(reservationEncounterId, patientEncounters.Select(e => (e.Id, e.Status, e.AdmitUtc, e.DischargeUtc, e.CurrentLocation)));

    private static string? For(
        long? reservationEncounterId,
        IEnumerable<(long Id, EncounterStatus Status, DateTime? AdmitUtc, DateTime? DischargeUtc, string? Location)> visits)
    {
        var open = visits
            .Where(v => v.Status == EncounterStatus.Active && v.DischargeUtc is null && !string.IsNullOrWhiteSpace(v.Location))
            .ToList();
        var chosen = open.Where(v => v.Id == reservationEncounterId).Select(v => v.Location).FirstOrDefault()
            ?? open.OrderByDescending(v => v.AdmitUtc ?? DateTime.MinValue).ThenByDescending(v => v.Id).Select(v => v.Location).FirstOrDefault();
        return chosen?.Trim();
    }
}
