using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Interfaces;

public static class InterfaceVendorCodes
{
    public const string Custom = "Custom";
    public const string Epic = "Epic";
    public const string Cerner = "Cerner";
    public const string Meditech = "Meditech";
    public const string EpicResolute = "EpicResolute";
    public const string CernerPatientAccounting = "CernerPatientAccounting";
}

public sealed record InterfaceVendorInfo(
    string Code,
    string Name,
    string Description,
    IReadOnlyList<InterfaceType> InterfaceTypes);

public sealed record InterfaceVendorConnectionDefaults(
    string? SendingApplication,
    string? SendingFacility,
    string? ReceivingApplication,
    string? ReceivingFacility);

public sealed record InterfaceVendorPreset(
    string VendorCode,
    string VendorName,
    InterfaceType InterfaceType,
    Hl7Direction Direction,
    InterfaceVendorConnectionDefaults Connection,
    IReadOnlyList<InterfaceFieldMappingDraft> Mappings);

public sealed record InterfaceFieldMappingDraft(string DataItemKey, string Hl7Path, bool IsRequired);

/// <summary>
/// Vendor-specific mapping and MSH identity presets. Host/port stay facility-entered.
/// </summary>
public static class InterfaceVendorPresets
{
    public static IReadOnlyList<InterfaceVendorInfo> All { get; } =
    [
        new(InterfaceVendorCodes.Epic, "Epic", "Epic EHR (ADT, orders, results, BPAM).",
            [InterfaceType.Adt, InterfaceType.Orders, InterfaceType.Results, InterfaceType.Bpam]),
        new(InterfaceVendorCodes.Cerner, "Cerner", "Oracle Health / Cerner EHR.",
            [InterfaceType.Adt, InterfaceType.Orders, InterfaceType.Results, InterfaceType.Bpam]),
        new(InterfaceVendorCodes.Meditech, "Meditech", "Meditech EHR.",
            [InterfaceType.Adt, InterfaceType.Orders, InterfaceType.Results]),
        new(InterfaceVendorCodes.EpicResolute, "Epic Resolute", "Epic Resolute hospital billing (DFT).",
            [InterfaceType.Billing]),
        new(InterfaceVendorCodes.CernerPatientAccounting, "Cerner Patient Accounting", "Cerner / Oracle Health billing (DFT).",
            [InterfaceType.Billing]),
        new(InterfaceVendorCodes.Custom, "Custom", "Blank catalog defaults; edit paths by hand.",
            [InterfaceType.Adt, InterfaceType.Billing, InterfaceType.Orders, InterfaceType.Results, InterfaceType.Bpam])
    ];

    public static IReadOnlyList<InterfaceVendorInfo> For(InterfaceType type) =>
        All.Where(v => v.InterfaceTypes.Contains(type)).ToList();

    public static InterfaceVendorPreset? Get(string vendorCode, InterfaceType type, Hl7Direction direction)
    {
        var vendor = All.FirstOrDefault(v => string.Equals(v.Code, vendorCode, StringComparison.OrdinalIgnoreCase));
        if (vendor is null || !vendor.InterfaceTypes.Contains(type))
        {
            return null;
        }

        var catalog = InterfaceDataItemCatalog.For(type, direction);
        var overrides = PathOverrides(vendor.Code, type);
        var mappings = catalog
            .Select(item => new InterfaceFieldMappingDraft(
                item.Key,
                overrides.TryGetValue(item.Key, out var path) ? path : item.DefaultHl7Path,
                item.Required))
            .ToList();

        return new InterfaceVendorPreset(
            vendor.Code,
            vendor.Name,
            type,
            direction,
            ConnectionDefaults(vendor.Code, direction),
            mappings);
    }

    private static InterfaceVendorConnectionDefaults ConnectionDefaults(string vendor, Hl7Direction direction)
    {
        var inbound = direction == Hl7Direction.Inbound;
        return vendor switch
        {
            InterfaceVendorCodes.Epic => inbound
                ? new("EPIC", "HOSP", "BloodBankLIS", "BBLIS")
                : new("BloodBankLIS", "BBLIS", "EPIC", "HOSP"),
            InterfaceVendorCodes.Cerner => inbound
                ? new("CERNER", "HOSP", "BloodBankLIS", "BBLIS")
                : new("BloodBankLIS", "BBLIS", "CERNER", "HOSP"),
            InterfaceVendorCodes.Meditech => inbound
                ? new("MEDITECH", "HOSP", "BloodBankLIS", "BBLIS")
                : new("BloodBankLIS", "BBLIS", "MEDITECH", "HOSP"),
            InterfaceVendorCodes.EpicResolute =>
                new("BloodBankLIS", "BBLIS", "EPIC", "RESOLUTE"),
            InterfaceVendorCodes.CernerPatientAccounting =>
                new("BloodBankLIS", "BBLIS", "CERNER", "PA"),
            _ => inbound
                ? new("EHR", "HOSP", "BloodBankLIS", "BBLIS")
                : new("BloodBankLIS", "BBLIS", "EHR", "HOSP")
        };
    }

    private static IReadOnlyDictionary<string, string> PathOverrides(string vendor, InterfaceType type)
    {
        if (string.Equals(vendor, InterfaceVendorCodes.Custom, StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, string>();
        }

        // Epic: CSN in PV1-19, HAR in PID-18 (already catalog defaults). Alternate id unused.
        // Cerner sites often put MRN in PID-2 and visit in PV1-19 (whole field).
        if (string.Equals(vendor, InterfaceVendorCodes.Cerner, StringComparison.OrdinalIgnoreCase)
            && type is InterfaceType.Adt or InterfaceType.Orders)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [InterfaceDataItemKeys.PatientMrn] = "PID-3-1",
                [InterfaceDataItemKeys.EncounterVisitNumber] = "PV1-19"
            };
        }

        if (string.Equals(vendor, InterfaceVendorCodes.Meditech, StringComparison.OrdinalIgnoreCase)
            && type == InterfaceType.Adt)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [InterfaceDataItemKeys.PatientMrn] = "PID-2",
                [InterfaceDataItemKeys.EncounterVisitNumber] = "PV1-19"
            };
        }

        if (string.Equals(vendor, InterfaceVendorCodes.Epic, StringComparison.OrdinalIgnoreCase)
            && type == InterfaceType.Bpam)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [InterfaceDataItemKeys.UnitNumber] = "RXA-15-1",
                [InterfaceDataItemKeys.UnitDin] = "RXA-15-1"
            };
        }

        return new Dictionary<string, string>();
    }
}
