using BloodBankLIS.Application.Reference;

namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>
/// What a new order can take from its visit. The ordering location is the active catalog location whose code, HL7
/// mapping code (as the interface resolves a location), or name is the visit's current location (else its admission location), and only when exactly one matches; otherwise the
/// form keeps what is chosen. The attending provider is offered, never filled in, because the ordering provider is
/// who the order is attributed to.
/// </summary>
public static class OrderVisitDefaults
{
    public static string? VisitLocation(EncounterDto? visit) =>
        new[] { visit?.CurrentLocation, visit?.AdmissionLocation }
            .Select(l => l?.Trim())
            .FirstOrDefault(l => !string.IsNullOrEmpty(l));

    public static long? LocationFor(EncounterDto? visit, IEnumerable<OrderingLocationRefDto> locations)
    {
        var place = VisitLocation(visit);
        if (place is null)
        {
            return null;
        }

        var matches = locations
            .Where(l => l.IsActive
                        && (string.Equals(l.Code?.Trim(), place, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(l.Hl7MappingCode?.Trim(), place, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(l.Name?.Trim(), place, StringComparison.OrdinalIgnoreCase)))
            .Select(l => l.Id)
            .Distinct()
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    public static OrderingProviderRefDto? AttendingFor(EncounterDto? visit, IEnumerable<OrderingProviderRefDto> providers) =>
        visit?.AttendingProviderId is long id ? providers.FirstOrDefault(p => p.Id == id) : null;
}
