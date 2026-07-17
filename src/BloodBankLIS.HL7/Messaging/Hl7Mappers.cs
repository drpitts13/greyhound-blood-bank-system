using System.Globalization;

using BloodBankLIS.Domain.Enums;

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

    string? AttendingProviderName);



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



/// <summary>Pure mapping of ADT PID/PV1 fields to demographics (docs/hl7-design.md 2.1).</summary>

public static class Hl7AdtMapper

{

    public static Hl7PatientData Map(Hl7Message message)

    {

        ArgumentNullException.ThrowIfNull(message);

        return new Hl7PatientData(

            Mrn: FirstNonEmpty(message.Get("PID-3-1"), message.Get("PID-2")),

            LastName: message.Get("PID-5-1"),

            FirstName: message.Get("PID-5-2"),

            MiddleName: NullIfEmpty(message.Get("PID-5-3")),

            DateOfBirth: ParseHl7Date(message.Get("PID-7")),

            Sex: MapSex(message.Get("PID-8")),

            TriggerEvent: message.TriggerEvent,

            VisitNumber: NullIfEmpty(FirstNonEmpty(message.Get("PV1-19-1"), message.Get("PV1-19"), message.Get("PV1-50"))),

            AccountNumber: NullIfEmpty(FirstNonEmpty(message.Get("PID-18-1"), message.Get("PID-18"))),

            AdmitUtc: ParseHl7DateTime(FirstNonEmpty(message.Get("PV1-44"), message.Get("PV1-44-1"))),

            DischargeUtc: ParseHl7DateTime(FirstNonEmpty(message.Get("PV1-45"), message.Get("PV1-45-1"))),

            CurrentLocation: NullIfEmpty(FirstNonEmpty(message.Get("PV1-3-1"), message.Get("PV1-3-2"), message.Get("PV1-3"))),

            AttendingProviderId: NullIfEmpty(message.Get("PV1-7-1")),

            AttendingProviderName: Hl7Xcn.FormatName(message, "PV1-7"));

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



    private static string FirstNonEmpty(params string[] values) =>

        values.FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? string.Empty;



    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

}



/// <summary>Pure mapping of ORM/OML ORC/OBR fields to an order (docs/hl7-design.md 2.2).</summary>

public static class Hl7OrmMapper

{

    public static Hl7OrderData Map(Hl7Message message)

    {

        ArgumentNullException.ThrowIfNull(message);

        var placer = FirstNonEmpty(message.Get("ORC-2-1"), message.Get("OBR-2-1"), message.Get("ORC-2"), message.Get("OBR-2"));

        return new Hl7OrderData(

            OrderControl: FirstNonEmpty(message.Get("ORC-1"), "NW"),

            PlacerOrderId: placer,

            Mrn: FirstNonEmpty(message.Get("PID-3-1"), message.Get("PID-2")),

            OrderType: MapOrderType(message.Get("OBR-4-1")),

            TestCode: NullIfEmpty(message.Get("OBR-4-1")),

            VisitNumber: NullIfEmpty(FirstNonEmpty(message.Get("PV1-19-1"), message.Get("PV1-19"))),

            OrderingProviderId: NullIfEmpty(message.Get("ORC-12-1")),

            OrderingProviderName: Hl7Xcn.FormatName(message, "ORC-12"),

            OrderingLocationCode: NullIfEmpty(FirstNonEmpty(message.Get("ORC-13-1"), message.Get("ORC-13"))));

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


