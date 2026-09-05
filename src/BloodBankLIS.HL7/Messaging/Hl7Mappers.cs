using System.Globalization;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Messaging;

/// <summary>Demographics and visit context extracted from an inbound ADT message (PID/PV1).</summary>
public sealed record Hl7PatientData(
    string Mrn,
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly? DateOfBirth,
    Sex Sex,
    string TriggerEvent,
    string? VisitNumber,
    string? AccountNumber,
    DateTime? AdmitUtc,
    DateTime? DischargeUtc,
    string? CurrentLocation,
    string? AttendingProviderId,
    string? AttendingProviderName,
    string? PriorMrn);

/// <summary>Order details extracted from an inbound ORM/OML message (ORC/OBR/PV1).</summary>
public sealed record Hl7OrderData(
    string OrderControl,
    string PlacerOrderId,
    string Mrn,
    OrderType OrderType,
    string? TestCode,
    string? VisitNumber,
    string? OrderingProviderId,
    string? OrderingProviderName,
    string? OrderingLocationCode);

/// <summary>Result details extracted from an inbound ORU message (PID/ORC/OBR/OBX).</summary>
public sealed record Hl7ResultData(
    string Mrn,
    string? PlacerOrderId,
    string TestCode,
    string Value,
    string? Units,
    string? Interpretation,
    string? ObxStatus);

/// <summary>Blood-product administration details extracted from an inbound RAS/BPS message.</summary>
public sealed record Hl7BpamData(
    string Mrn,
    string? UnitNumber,
    string? Din,
    DateTime? StartUtc,
    DateTime? StopUtc,
    decimal? VolumeTransfused,
    string? Location,
    string? Transfusionist,
    bool ReactionSuspected);

/// <summary>Pure mapping of ADT PID/PV1 fields to demographics (docs/hl7-design.md 2.1).</summary>
public static class Hl7AdtMapper
{
    public static Hl7PatientData Map(Hl7Message message, Hl7FieldMap? map = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        map ??= Hl7FieldMap.Default(InterfaceType.Adt, Hl7Direction.Inbound);

        return new Hl7PatientData(
            Mrn: map.Get(message, InterfaceDataItemKeys.PatientMrn),
            LastName: map.Get(message, InterfaceDataItemKeys.PatientLastName),
            FirstName: map.Get(message, InterfaceDataItemKeys.PatientFirstName),
            MiddleName: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.PatientMiddleName)),
            DateOfBirth: ParseHl7Date(map.Get(message, InterfaceDataItemKeys.PatientDateOfBirth)),
            Sex: MapSex(map.Get(message, InterfaceDataItemKeys.PatientSex)),
            TriggerEvent: message.TriggerEvent,
            VisitNumber: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.EncounterVisitNumber)),
            AccountNumber: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.EncounterAccountNumber)),
            AdmitUtc: ParseHl7DateTime(map.Get(message, InterfaceDataItemKeys.EncounterAdmitUtc)),
            DischargeUtc: ParseHl7DateTime(map.Get(message, InterfaceDataItemKeys.EncounterDischargeUtc)),
            CurrentLocation: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.EncounterCurrentLocation)),
            AttendingProviderId: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.EncounterAttendingProviderId)),
            AttendingProviderName: MapPersonName(message, map, InterfaceDataItemKeys.EncounterAttendingProviderName),
            PriorMrn: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.PatientPriorMrn)));
    }

    public static Sex MapSex(string code) => code.ToUpperInvariant() switch
    {
        "M" => Sex.Male,
        "F" => Sex.Female,
        "O" => Sex.Other,
        _ => Sex.Unknown
    };

    internal static DateOnly? ParseHl7Date(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 8)
        {
            return null;
        }

        return DateOnly.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    internal static DateTime? ParseHl7DateTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 8)
        {
            return null;
        }

        if (!DateOnly.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        if (value.Length >= 12
            && int.TryParse(value.AsSpan(8, 2), out var hour)
            && int.TryParse(value.AsSpan(10, 2), out var minute))
        {
            return new DateTime(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Utc);
        }

        return date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }

    internal static string? MapPersonName(Hl7Message message, Hl7FieldMap map, string key)
    {
        var path = map.Path(key);
        if (path.Count(c => c == '-') == 1)
        {
            return Hl7Xcn.FormatName(message, path);
        }

        return NullIfEmpty(map.Get(message, key));
    }

    internal static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}

/// <summary>Pure mapping of ORM/OML ORC/OBR fields to an order (docs/hl7-design.md 2.2).</summary>
public static class Hl7OrmMapper
{
    public static Hl7OrderData Map(Hl7Message message, Hl7FieldMap? map = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        map ??= Hl7FieldMap.Default(InterfaceType.Orders, Hl7Direction.Inbound);

        var placer = map.Get(message, InterfaceDataItemKeys.OrderNumber);
        var testCode = NullIfEmpty(map.Get(message, InterfaceDataItemKeys.OrderTestCode));

        return new Hl7OrderData(
            OrderControl: FirstNonEmpty(map.Get(message, InterfaceDataItemKeys.OrderControl), "NW"),
            PlacerOrderId: placer,
            Mrn: map.Get(message, InterfaceDataItemKeys.PatientMrn),
            OrderType: MapOrderType(testCode ?? string.Empty),
            TestCode: testCode,
            VisitNumber: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.EncounterVisitNumber)),
            OrderingProviderId: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.OrderProviderId)),
            OrderingProviderName: Hl7AdtMapper.MapPersonName(message, map, InterfaceDataItemKeys.OrderProviderName),
            OrderingLocationCode: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.OrderLocationCode)));
    }

    public static OrderType MapOrderType(string universalServiceId) => universalServiceId.ToUpperInvariant() switch
    {
        "TS" or "T&S" or "TYPESCREEN" or "TYPEANDSCREEN" => OrderType.TypeAndScreen,
        "XM" or "CROSSMATCH" => OrderType.Crossmatch,
        "ABID" or "ANTIBODYID" => OrderType.AntibodyIdentification,
        "AGTYPE" or "ANTIGEN" => OrderType.AntigenTyping,
        "DAT" => OrderType.DirectAntiglobulinTest,
        _ => OrderType.Other
    };

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? string.Empty;

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}

/// <summary>Pure mapping of inbound ORU observation fields (docs/hl7-design.md 2.3a).</summary>
public static class Hl7OruMapper
{
    public static Hl7ResultData Map(Hl7Message message, Hl7FieldMap? map = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        map ??= Hl7FieldMap.Default(InterfaceType.Results, Hl7Direction.Inbound);

        var testCode = FirstCoded(
            map.Get(message, InterfaceDataItemKeys.ResultObrTestCode),
            map.Get(message, InterfaceDataItemKeys.ResultObxIdentifier));

        return new Hl7ResultData(
            Mrn: map.Get(message, InterfaceDataItemKeys.PatientMrn),
            PlacerOrderId: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.OrderNumber)),
            TestCode: testCode,
            Value: FirstCoded(map.Get(message, InterfaceDataItemKeys.ResultValue)),
            Units: NullIfEmpty(FirstCoded(map.Get(message, InterfaceDataItemKeys.ResultUnits))),
            Interpretation: NullIfEmpty(FirstCoded(map.Get(message, InterfaceDataItemKeys.ResultInterpretation))),
            ObxStatus: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.ResultObxStatus)));
    }

    private static string FirstCoded(params string[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var code = value.Split('^', 2)[0].Trim();
            if (code.Length > 0)
            {
                return code;
            }
        }

        return string.Empty;
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}

/// <summary>Pure mapping of RAS/BPS blood-product administration fields.</summary>
public static class Hl7BpamMapper
{
    public static Hl7BpamData Map(Hl7Message message, Hl7FieldMap? map = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        map ??= Hl7FieldMap.Default(InterfaceType.Bpam, Hl7Direction.Inbound);

        var volumeRaw = map.Get(message, InterfaceDataItemKeys.TransfusionVolume);
        decimal? volume = decimal.TryParse(volumeRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

        var reactionRaw = map.Get(message, InterfaceDataItemKeys.TransfusionReaction);
        var reaction = reactionRaw.Contains('Y', StringComparison.OrdinalIgnoreCase)
            || reactionRaw.Contains("REACTION", StringComparison.OrdinalIgnoreCase);

        return new Hl7BpamData(
            Mrn: map.Get(message, InterfaceDataItemKeys.PatientMrn),
            UnitNumber: Hl7AdtMapper.NullIfEmpty(map.Get(message, InterfaceDataItemKeys.UnitNumber)),
            Din: Hl7AdtMapper.NullIfEmpty(map.Get(message, InterfaceDataItemKeys.UnitDin)),
            StartUtc: Hl7AdtMapper.ParseHl7DateTime(map.Get(message, InterfaceDataItemKeys.TransfusionStartUtc)),
            StopUtc: Hl7AdtMapper.ParseHl7DateTime(map.Get(message, InterfaceDataItemKeys.TransfusionStopUtc)),
            VolumeTransfused: volume,
            Location: Hl7AdtMapper.NullIfEmpty(map.Get(message, InterfaceDataItemKeys.TransfusionLocation)),
            Transfusionist: Hl7AdtMapper.MapPersonName(message, map, InterfaceDataItemKeys.Transfusionist)
                ?? Hl7AdtMapper.NullIfEmpty(map.Get(message, InterfaceDataItemKeys.Transfusionist)),
            ReactionSuspected: reaction);
    }
}

internal static class Hl7Xcn
{
    public static string? FormatName(Hl7Message message, string fieldPrefix)
    {
        var family = message.Get($"{fieldPrefix}-2");
        var given = message.Get($"{fieldPrefix}-3");
        if (!string.IsNullOrEmpty(family) && !string.IsNullOrEmpty(given))
        {
            return $"{family}, {given}";
        }

        var full = message.Get(fieldPrefix);
        return string.IsNullOrEmpty(full) ? null : full.Replace('^', ' ').Trim();
    }
}
