using System.Globalization;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Domain.Rules;
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
    string? PriorMrn,
    // Local codes after translation; null when the field was empty or did not translate to a local value.
    string? Race = null,
    string? Ethnicity = null,
    EncounterType? PatientClass = null,
    // PID-3 repetitions other than the MRN (enterprise id, other facilities' numbers), kept as alternate identifiers.
    IReadOnlyList<Hl7PatientIdentifier>? OtherIdentifiers = null,
    // PID-21 Mother's Identifier, for a newborn: the mother's MRN.
    string? MotherMrn = null);

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
    string? OrderingLocationCode,
    OrderPriority Priority = OrderPriority.Routine,
    // TQ1-7 (or OBR-27-4) start date/time: when the order is needed.
    DateTime? NeededByUtc = null,
    // ORC-16: why the order was cancelled or discontinued (text, else code).
    string? ControlReason = null);

/// <summary>Result details extracted from an inbound ORU message (PID/ORC/OBR/OBX).</summary>
public sealed record Hl7ResultData(
    string Mrn,
    string? PlacerOrderId,
    string TestCode,
    string Value,
    string? Units,
    string? Interpretation,
    string? ObxStatus,
    string? SpecimenAccession = null);

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
            Mrn: Hl7PatientIdentifiers.Mrn(message, map),
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
            PriorMrn: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.PatientPriorMrn)),
            Race: MapLocal(map, message, InterfaceDataItemKeys.PatientRace, PatientDemographicCodes.NormalizeRace),
            Ethnicity: MapLocal(map, message, InterfaceDataItemKeys.PatientEthnicity, PatientDemographicCodes.NormalizeEthnicity),
            PatientClass: MapPatientClass(map, message),
            OtherIdentifiers: Hl7PatientIdentifiers.Others(message, map),
            MotherMrn: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.PatientMotherMrn)));
    }

    /// <summary>
    /// A coded field translated to one of the LIS's local values. A value that does not translate to one is listed as
    /// unmapped for the interface team and not stored.
    /// </summary>
    private static string? MapLocal(Hl7FieldMap map, Hl7Message message, string key, Func<string?, string?> normalize)
    {
        var value = map.Get(message, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var local = normalize(value);
        if (local is null)
        {
            map.Translator.RecordMiss(key, value, InterfaceTranslationDirection.Inbound);
        }

        return local;
    }

    /// <summary>PV1-2 translated to a visit type (Inpatient, Outpatient, Emergency…); unmapped values are listed.</summary>
    private static EncounterType? MapPatientClass(Hl7FieldMap map, Hl7Message message)
    {
        var value = map.Get(message, InterfaceDataItemKeys.EncounterPatientClass);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<EncounterType>(value.Trim(), ignoreCase: true, out var type) && !int.TryParse(value.Trim(), out _))
        {
            return type;
        }

        map.Translator.RecordMiss(InterfaceDataItemKeys.EncounterPatientClass, value, InterfaceTranslationDirection.Inbound);
        return null;
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

    /// <summary>
    /// An HL7 TS (YYYYMMDD[HHMM[SS[.S…]]][+/-ZZZZ]) as UTC. A stated offset is applied (end-user testing pass 62: it
    /// was ignored, along with the seconds, so "20261113070000-0500" was stored as 07:00 UTC instead of 12:00 UTC). A
    /// time with no offset is taken as UTC, the interface's convention in both directions (outbound times are written
    /// in UTC); whether an EHR's offset-less local times should be read in the facility time zone is OCD-088.
    /// </summary>
    internal static DateTime? ParseHl7DateTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 8)
        {
            return null;
        }

        value = value.Trim();
        TimeSpan? offset = null;
        var sign = value.IndexOfAny(['+', '-'], 8);
        if (sign > 0)
        {
            var zone = value[(sign + 1)..];
            if (zone.Length == 4
                && int.TryParse(zone.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var zh)
                && int.TryParse(zone.AsSpan(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var zm)
                && zh <= 14 && zm < 60)
            {
                offset = new TimeSpan(zh, zm, 0) * (value[sign] == '-' ? -1 : 1);
            }

            value = value[..sign];
        }

        if (!DateOnly.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        var local = date.ToDateTime(TimeOnly.MinValue);
        if (value.Length >= 12
            && int.TryParse(value.AsSpan(8, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hour)
            && int.TryParse(value.AsSpan(10, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var minute)
            && hour < 24 && minute < 60)
        {
            local = local.AddHours(hour).AddMinutes(minute);
            if (value.Length >= 14
                && int.TryParse(value.AsSpan(12, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var second)
                && second < 60)
            {
                local = local.AddSeconds(second);
            }
        }

        return DateTime.SpecifyKind(offset is TimeSpan o ? local - o : local, DateTimeKind.Utc);
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
            Mrn: Hl7PatientIdentifiers.Mrn(message, map),
            OrderType: MapOrderType(testCode ?? string.Empty),
            TestCode: testCode,
            VisitNumber: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.EncounterVisitNumber)),
            OrderingProviderId: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.OrderProviderId)),
            OrderingProviderName: Hl7AdtMapper.MapPersonName(message, map, InterfaceDataItemKeys.OrderProviderName),
            OrderingLocationCode: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.OrderLocationCode)),
            Priority: MapPriority(map.Get(message, InterfaceDataItemKeys.OrderPriority)),
            NeededByUtc: Hl7AdtMapper.ParseHl7DateTime(map.Get(message, InterfaceDataItemKeys.OrderNeededBy)),
            ControlReason: NullIfEmpty(FirstNonEmpty(map.Get(message, InterfaceDataItemKeys.OrderControlReason), message.Get("ORC-16-1")).Trim()));
    }

    /// <summary>
    /// HL7 priority (table 0027 and common text). A blank or unrecognized code stays Routine.
    /// </summary>
    public static OrderPriority MapPriority(string? code)
    {
        var token = (code ?? string.Empty).Split('^', 2)[0].Trim().ToUpperInvariant();
        return token switch
        {
            "S" or "STAT" => OrderPriority.Stat,
            "A" or "ASAP" => OrderPriority.Urgent,
            "T" or "TIMING" => OrderPriority.Timed,
            "P" or "PREOP" or "PRE-OP" => OrderPriority.PreOp,
            _ => OrderPriority.Routine
        };
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
            Mrn: Hl7PatientIdentifiers.Mrn(message, map),
            PlacerOrderId: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.OrderNumber)),
            TestCode: testCode,
            Value: FirstCoded(map.Get(message, InterfaceDataItemKeys.ResultValue)),
            Units: NullIfEmpty(FirstCoded(map.Get(message, InterfaceDataItemKeys.ResultUnits))),
            Interpretation: NullIfEmpty(FirstCoded(map.Get(message, InterfaceDataItemKeys.ResultInterpretation))),
            ObxStatus: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.ResultObxStatus)),
            SpecimenAccession: NullIfEmpty(map.Get(message, InterfaceDataItemKeys.ResultSpecimenAccession)));
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

/// <summary>An OMB^O27 request to send a reserved unit now (OCD-063).</summary>
public sealed record Hl7DispenseRequestData(
    string Mrn,
    bool Cancel,
    string? OrderNumber,
    string? Din,
    string? Destination,
    string? Requester);

/// <summary>Pure mapping of an OMB^O27 dispense request. The DIN is read only when the facility has mapped a field to it.</summary>
public static class Hl7DispenseRequestMapper
{
    public static Hl7DispenseRequestData Map(Hl7Message message, Hl7FieldMap? map = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        map ??= Hl7FieldMap.Default(InterfaceType.Bpam, Hl7Direction.Inbound);
        var control = map.Get(message, InterfaceDataItemKeys.DispenseOrderControl).Trim().ToUpperInvariant();
        var dinPath = map.Path(InterfaceDataItemKeys.DispenseDin);
        return new Hl7DispenseRequestData(
            Mrn: Hl7PatientIdentifiers.Mrn(message, map),
            Cancel: control is "CA" or "OC" or "DC",
            OrderNumber: Hl7AdtMapper.NullIfEmpty(map.Get(message, InterfaceDataItemKeys.DispenseOrderNumber)),
            Din: string.IsNullOrWhiteSpace(dinPath) ? null : Hl7AdtMapper.NullIfEmpty(message.Get(dinPath)),
            Destination: Hl7AdtMapper.NullIfEmpty(map.Get(message, InterfaceDataItemKeys.DispenseDestination))
                ?? Hl7AdtMapper.NullIfEmpty(map.Get(message, InterfaceDataItemKeys.DispensePatientLocation)),
            Requester: Hl7AdtMapper.MapPersonName(message, map, InterfaceDataItemKeys.DispenseRequester)
                ?? Hl7AdtMapper.NullIfEmpty(map.Get(message, InterfaceDataItemKeys.DispenseRequester)));
    }
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
            Mrn: Hl7PatientIdentifiers.Mrn(message, map),
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
