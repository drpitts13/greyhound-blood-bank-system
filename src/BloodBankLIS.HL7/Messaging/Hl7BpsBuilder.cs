using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Messaging;

/// <summary>
/// Builds an outbound BPS^O29 blood product dispense status message (MSH, PID, PV1, ORC, BPO, BPX) for a unit issued
/// to, or returned from, a patient. Dispense status codes follow HL7 v2.5 table 0510 (DS dispensed to patient
/// location, RA returned unused) and can be translated per site under the <see cref="StatusKey"/> data item.
/// </summary>
public static class Hl7BpsBuilder
{
    public const string StatusKey = "BloodProduct.DispenseStatus";
    public const string Dispensed = "DS";
    public const string Returned = "RA";

    public static string StatusCode(ProductDispenseEvent dispenseEvent) =>
        dispenseEvent == ProductDispenseEvent.Returned ? Returned : Dispensed;

    public static string Build(
        Patient patient,
        Issue issue,
        BloodUnit unit,
        ProductType? product,
        Order? order,
        ProductDispenseEvent dispenseEvent,
        DateTime statusUtc,
        string controlId,
        DateTime nowUtc,
        Hl7OutboundIdentity? identity = null,
        Hl7Encoding? encoding = null,
        Hl7FieldMap? map = null,
        InterfaceValueTranslator? translator = null,
        EncounterType? visitType = null)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(unit);
        var id = identity ?? new Hl7OutboundIdentity();
        map ??= Hl7FieldMap.Default(InterfaceType.Bpam, Hl7Direction.Outbound);
        translator ??= InterfaceValueTranslator.Empty;
        var status = translator.ToExternal(StatusKey, StatusCode(dispenseEvent));
        var dispensed = dispenseEvent == ProductDispenseEvent.Dispensed;
        var productCode = product?.ProductCode ?? unit.ProductCodeData;

        var builder = new Hl7PathMessageBuilder(encoding)
            .AppendMsh(id.SendingApp, id.SendingFacility, id.ReceivingApp, id.ReceivingFacility, nowUtc, "BPS^O29^BPS_O29", controlId)
            .EnsureSegment("PID").EnsureSegment("PV1").EnsureSegment("ORC").EnsureSegment("BPO").EnsureSegment("BPX")
            .Set("PID-1", "1")
            .Set(Path(map, InterfaceDataItemKeys.PatientMrn, "PID-3-1"), translator.ToExternal(InterfaceDataItemKeys.PatientMrn, patient.MedicalRecordNumber))
            .Set("PID-5-1", patient.LastName)
            .Set("PID-5-2", patient.FirstName)
            .Set("PID-7", patient.DateOfBirth.ToString("yyyyMMdd"))
            .Set("PID-10-1", translator.ToExternal(InterfaceDataItemKeys.PatientRace, patient.Race))
            .Set("PID-22-1", translator.ToExternal(InterfaceDataItemKeys.PatientEthnicity, patient.Ethnicity))
            .Set("PV1-1", "1")
            // PV1-2 patient class from the issue's visit, translated; U when no visit type is known.
            .Set("PV1-2", visitType is EncounterType known && known != EncounterType.Unknown
                ? translator.ToExternal(InterfaceDataItemKeys.EncounterPatientClass, known.ToString())
                : "U")
            .Set("PV1-3-1", issue.IssuedToLocation)
            // SC: status changed. The placer number is the EHR's order when the unit was issued against one.
            .Set("ORC-1", "SC")
            .Set("ORC-2", order?.OrderNumber)
            .Set("ORC-3", order?.FillerOrderNumber ?? $"ISS{issue.Id}")
            .Set("BPO-1", "1")
            .Set("BPO-2-1", productCode)
            .Set("BPO-2-2", product?.Name)
            .Set("BPO-4", "1")
            .Set("BPX-1", "1")
            .Set("BPX-2-1", status)
            .Set("BPX-2-2", dispensed ? "Dispensed to patient location" : "Returned unused")
            .Set("BPX-2-3", "HL70510")
            .Set("BPX-3", "F")
            .Set("BPX-4", statusUtc.ToString(Hl7MessageBuilder.Hl7TimestampFormat))
            .Set("BPX-5", unit.Din ?? unit.UnitNumber)
            .Set("BPX-6-1", productCode)
            .Set("BPX-6-2", product?.Name)
            .Set("BPX-11-1", unit.AboRhdCode)
            .Set("BPX-11-2", unit.BloodType.ToString())
            .Set("BPX-14", "1")
            .Set("BPX-17", unit.UnitNumber);
        if (dispensed)
        {
            builder
                .Set("BPX-18-1", issue.IssuedToLocation)
                .Set("BPX-20-2", issue.IssuedTo)
                .Set("BPX-21-2", issue.IssuedBy);
        }

        return builder.Build();
    }

    private static string Path(Hl7FieldMap map, string key, string fallback)
    {
        var path = map.Path(key);
        return string.IsNullOrWhiteSpace(path) ? fallback : path;
    }
}
