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
    public static string? For(long? reservationEncounterId, IEnumerable<Encounter> patientEncounters)
    {
        var open = patientEncounters
            .Where(e => e.Status == EncounterStatus.Active && e.DischargeUtc is null && !string.IsNullOrWhiteSpace(e.CurrentLocation))
            .ToList();
        var chosen = open.FirstOrDefault(e => e.Id == reservationEncounterId)
            ?? open.OrderByDescending(e => e.AdmitUtc ?? DateTime.MinValue).ThenByDescending(e => e.Id).FirstOrDefault();
        return chosen?.CurrentLocation!.Trim();
    }
}
