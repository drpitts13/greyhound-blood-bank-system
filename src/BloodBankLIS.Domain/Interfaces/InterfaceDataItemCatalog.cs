using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Interfaces;

/// <summary>One mappable application field for an interface type.</summary>
public sealed record InterfaceDataItem(
    string Key,
    string DisplayName,
    string Description,
    string DefaultHl7Path,
    bool Required);

/// <summary>
/// Application data items that can be mapped to HL7 paths, grouped by interface type.
/// Default paths match the original in-house ADT/ORM/ORU/DFT field locations.
/// </summary>
public static class InterfaceDataItemCatalog
{
    public static IReadOnlyList<InterfaceDataItem> For(InterfaceType type, Hl7Direction direction)
    {
        _ = direction;
        return type switch
        {
            InterfaceType.Adt => Adt,
            InterfaceType.Orders => Orders,
            InterfaceType.Results => Results,
            InterfaceType.Billing => Billing,
            InterfaceType.Bpam => Bpam,
            _ => Adt
        };
    }

    public static InterfaceDataItem? Find(InterfaceType type, Hl7Direction direction, string key) =>
        For(type, direction).FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.Ordinal));

    public static string DefaultPath(InterfaceType type, Hl7Direction direction, string key) =>
        Find(type, direction, key)?.DefaultHl7Path ?? string.Empty;

    /// <summary>Distinct catalog items across all interface types, ordered by display name.</summary>
    public static IReadOnlyList<InterfaceDataItem> AllDistinct() =>
        Enum.GetValues<InterfaceType>()
            .SelectMany(t => For(t, Hl7Direction.Inbound))
            .GroupBy(i => i.Key, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static bool ContainsKey(string key) =>
        Enum.GetValues<InterfaceType>().Any(t => Find(t, Hl7Direction.Inbound, key) is not null);

    private static readonly IReadOnlyList<InterfaceDataItem> PatientDemographics =
    [
        new(InterfaceDataItemKeys.PatientMrn, "Medical record number", "Patient MRN.", "PID-3-1", true),
        new(InterfaceDataItemKeys.PatientLastName, "Last name", "Patient family name.", "PID-5-1", true),
        new(InterfaceDataItemKeys.PatientFirstName, "First name", "Patient given name.", "PID-5-2", true),
        new(InterfaceDataItemKeys.PatientMiddleName, "Middle name", "Patient middle name.", "PID-5-3", false),
        new(InterfaceDataItemKeys.PatientDateOfBirth, "Date of birth", "Patient date of birth.", "PID-7", false),
        new(InterfaceDataItemKeys.PatientSex, "Sex", "Patient sex code (M/F/O/U).", "PID-8", false)
    ];

    private static readonly IReadOnlyList<InterfaceDataItem> Adt =
    [
        ..PatientDemographics,
        new(InterfaceDataItemKeys.EncounterAccountNumber, "Account number", "Hospital account / HAR.", "PID-18-1", false),
        new(InterfaceDataItemKeys.EncounterVisitNumber, "Visit number", "Visit / CSN.", "PV1-19-1", false),
        new(InterfaceDataItemKeys.EncounterAdmitUtc, "Admit date/time", "Admission timestamp.", "PV1-44", false),
        new(InterfaceDataItemKeys.EncounterDischargeUtc, "Discharge date/time", "Discharge timestamp.", "PV1-45", false),
        new(InterfaceDataItemKeys.EncounterCurrentLocation, "Current location", "Patient location.", "PV1-3-1", false),
        new(InterfaceDataItemKeys.EncounterAttendingProviderId, "Attending provider ID", "Attending provider identifier.", "PV1-7-1", false),
        new(InterfaceDataItemKeys.EncounterAttendingProviderName, "Attending provider name", "Attending provider name (XCN).", "PV1-7", false)
    ];

    private static readonly IReadOnlyList<InterfaceDataItem> Orders =
    [
        new(InterfaceDataItemKeys.OrderControl, "Order control", "ORC-1 (NW, CA, …).", "ORC-1", true),
        new(InterfaceDataItemKeys.OrderNumber, "Placer order number", "Placer order id.", "ORC-2-1", true),
        new(InterfaceDataItemKeys.PatientMrn, "Medical record number", "Patient MRN.", "PID-3-1", true),
        new(InterfaceDataItemKeys.OrderTestCode, "Test / service code", "Universal service id.", "OBR-4-1", true),
        new(InterfaceDataItemKeys.EncounterVisitNumber, "Visit number", "Visit / CSN.", "PV1-19-1", false),
        new(InterfaceDataItemKeys.OrderProviderId, "Ordering provider ID", "Ordering provider identifier.", "ORC-12-1", false),
        new(InterfaceDataItemKeys.OrderProviderName, "Ordering provider name", "Ordering provider name (XCN).", "ORC-12", false),
        new(InterfaceDataItemKeys.OrderLocationCode, "Ordering location", "Ordering location code.", "ORC-13-1", false)
    ];

    private static readonly IReadOnlyList<InterfaceDataItem> Results =
    [
        ..PatientDemographics,
        new(InterfaceDataItemKeys.ResultObrTestCode, "OBR test code", "Observation request identifier.", "OBR-4", true),
        new(InterfaceDataItemKeys.ResultVerifiedUtc, "Verified date/time", "Observation date/time.", "OBR-7", false),
        new(InterfaceDataItemKeys.ResultObxIdentifier, "OBX identifier", "Observation identifier.", "OBX-3", true),
        new(InterfaceDataItemKeys.ResultValue, "Result value", "Observation value.", "OBX-5", true),
        new(InterfaceDataItemKeys.ResultUnits, "Units", "Observation units.", "OBX-6", false),
        new(InterfaceDataItemKeys.ResultInterpretation, "Interpretation", "Abnormal flags / interpretation.", "OBX-8", false),
        new(InterfaceDataItemKeys.ResultObxStatus, "Result status", "OBX observation result status.", "OBX-11", true)
    ];

    private static readonly IReadOnlyList<InterfaceDataItem> Billing =
    [
        ..PatientDemographics,
        new(InterfaceDataItemKeys.BillingServiceDate, "Service date", "Transaction date.", "FT1-4", true),
        new(InterfaceDataItemKeys.BillingTransactionType, "Transaction type", "Typically CG (charge).", "FT1-6", true),
        new(InterfaceDataItemKeys.BillingCode, "Billing code", "Transaction / charge code.", "FT1-7", true),
        new(InterfaceDataItemKeys.BillingQuantity, "Quantity", "Transaction quantity.", "FT1-9", false)
    ];

    private static readonly IReadOnlyList<InterfaceDataItem> Bpam =
    [
        new(InterfaceDataItemKeys.PatientMrn, "Medical record number", "Patient MRN.", "PID-3-1", true),
        new(InterfaceDataItemKeys.UnitNumber, "Unit number", "Issued unit / product number.", "RXA-15", true),
        new(InterfaceDataItemKeys.UnitDin, "DIN", "ISBT donation identification number.", "RXA-15", false),
        new(InterfaceDataItemKeys.TransfusionStartUtc, "Start date/time", "Administration start.", "RXA-3", false),
        new(InterfaceDataItemKeys.TransfusionStopUtc, "Stop date/time", "Administration end.", "RXA-4", false),
        new(InterfaceDataItemKeys.TransfusionVolume, "Volume transfused", "Administered amount.", "RXA-6", false),
        new(InterfaceDataItemKeys.TransfusionLocation, "Location", "Administration location.", "PV1-3-1", false),
        new(InterfaceDataItemKeys.Transfusionist, "Transfusionist", "Administering provider.", "RXA-10-2", false),
        new(InterfaceDataItemKeys.TransfusionReaction, "Reaction suspected", "Reaction / completion flag.", "RXA-18", false)
    ];
}
