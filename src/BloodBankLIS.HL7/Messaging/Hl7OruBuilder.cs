using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Messaging;

/// <summary>Facility identifiers used when composing outbound messages.</summary>
public sealed record Hl7OutboundIdentity(
    string SendingApp = "BloodBankLIS",
    string SendingFacility = "BBLIS",
    string ReceivingApp = "EHR",
    string ReceivingFacility = "HOSP")
{
    public static Hl7OutboundIdentity From(InterfaceEndpoint? endpoint)
    {
        if (endpoint is null)
        {
            return new Hl7OutboundIdentity();
        }

        return new Hl7OutboundIdentity(
            string.IsNullOrWhiteSpace(endpoint.SendingApplication) ? "BloodBankLIS" : endpoint.SendingApplication,
            string.IsNullOrWhiteSpace(endpoint.SendingFacility) ? "BBLIS" : endpoint.SendingFacility,
            string.IsNullOrWhiteSpace(endpoint.ReceivingApplication) ? "EHR" : endpoint.ReceivingApplication,
            string.IsNullOrWhiteSpace(endpoint.ReceivingFacility) ? "HOSP" : endpoint.ReceivingFacility);
    }
}

/// <summary>
/// Builds an outbound ORU^R01 result message (MSH + PID + OBR + OBX) for a result released to the EHR.
/// Queued on request through <c>POST /api/hl7/outbound/results/{id}</c>. OBX-11 says whether it is a final
/// result (F), a correction (C), or a result withdrawn as wrong (W); see <see cref="ResultReleaseRule"/>.
/// </summary>
public static class Hl7OruBuilder
{
    public static string Build(
        Patient patient,
        TestResult result,
        string controlId,
        DateTime nowUtc,
        Hl7OutboundIdentity? identity = null,
        Hl7Encoding? encoding = null,
        Hl7FieldMap? map = null,
        InterfaceValueTranslator? translator = null)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(result);
        var id = identity ?? new Hl7OutboundIdentity();
        map ??= Hl7FieldMap.Default(InterfaceType.Results, Hl7Direction.Outbound);
        translator ??= InterfaceValueTranslator.Empty;

        var builder = new Hl7PathMessageBuilder(encoding)
            .AppendMsh(id.SendingApp, id.SendingFacility, id.ReceivingApp, id.ReceivingFacility, nowUtc, "ORU^R01", controlId)
            .EnsureSegment("PID").EnsureSegment("OBR").EnsureSegment("OBX")
            .Set("PID-1", "1")
            .Set(map.Path(InterfaceDataItemKeys.PatientMrn), translator.ToExternal(InterfaceDataItemKeys.PatientMrn, patient.MedicalRecordNumber))
            .Set(map.Path(InterfaceDataItemKeys.PatientLastName), translator.ToExternal(InterfaceDataItemKeys.PatientLastName, patient.LastName))
            .Set(map.Path(InterfaceDataItemKeys.PatientFirstName), translator.ToExternal(InterfaceDataItemKeys.PatientFirstName, patient.FirstName))
            .Set(map.Path(InterfaceDataItemKeys.PatientMiddleName), translator.ToExternal(InterfaceDataItemKeys.PatientMiddleName, patient.MiddleName))
            .Set(map.Path(InterfaceDataItemKeys.PatientDateOfBirth), translator.ToExternal(InterfaceDataItemKeys.PatientDateOfBirth, patient.DateOfBirth.ToString("yyyyMMdd")))
            .Set(map.Path(InterfaceDataItemKeys.PatientSex), translator.ToExternal(InterfaceDataItemKeys.PatientSex, SexCode(patient.Sex)))
            .Set("OBR-1", "1")
            .Set(map.Path(InterfaceDataItemKeys.ResultObrTestCode), translator.ToExternal(InterfaceDataItemKeys.ResultObrTestCode, result.TestCode))
            .Set(map.Path(InterfaceDataItemKeys.ResultVerifiedUtc), translator.ToExternal(InterfaceDataItemKeys.ResultVerifiedUtc, (result.VerifiedUtc ?? nowUtc).ToString(Hl7MessageBuilder.Hl7TimestampFormat)))
            .Set("OBX-1", "1")
            .Set("OBX-2", ObxValueType(result))
            .Set(map.Path(InterfaceDataItemKeys.ResultObxIdentifier), translator.ToExternal(InterfaceDataItemKeys.ResultObxIdentifier, result.TestCode))
            .Set(map.Path(InterfaceDataItemKeys.ResultValue), translator.ToExternal(InterfaceDataItemKeys.ResultValue, ObxValue(result)))
            .Set(map.Path(InterfaceDataItemKeys.ResultUnits), translator.ToExternal(InterfaceDataItemKeys.ResultUnits, result.Units))
            .Set(map.Path(InterfaceDataItemKeys.ResultInterpretation), translator.ToExternal(InterfaceDataItemKeys.ResultInterpretation, result.Interpretation))
            .Set(map.Path(InterfaceDataItemKeys.ResultObxStatus), translator.ToExternal(InterfaceDataItemKeys.ResultObxStatus,
                ResultReleaseRule.ObxStatus(result.Status, result.Version, result.VerifiedUtc is not null) ?? ResultReleaseRule.Final));
        if (!string.IsNullOrWhiteSpace(result.AbnormalFlag))
        {
            // OBX-8 abnormal flags (HL7 table 0078): L, H, LL, HH from the test's reference and critical limits.
            builder.Set("OBX-8", result.AbnormalFlag);
        }

        return builder.Build();
    }

    /// <summary>NM for a numeric result (a number with units or a range flag); ST for everything else, titers included.</summary>
    public static string ObxValueType(TestResult result) =>
        (!string.IsNullOrWhiteSpace(result.Units) || !string.IsNullOrWhiteSpace(result.AbnormalFlag))
        && decimal.TryParse(result.Value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _)
            ? "NM"
            : "ST";

    /// <summary>The value as the EHR reads it: a titer as its endpoint ("1:32"), anything else as stored.</summary>
    public static string? ObxValue(TestResult result) =>
        TiterResultValue.TryParse(result.Value, out var titer) ? titer!.EndpointLabel : result.Value;

    /// <summary>OBR-25 and OBX-11 value meaning no result can be obtained for the order (HL7 tables 0123 and 0085).</summary>
    public const string NoResult = "X";

    /// <summary>
    /// OCD-062: tells the EHR a test will have no result because its specimen was rejected. ORU^R01 with OBR-25 = X and
    /// one OBX (OBX-11 = X) whose text gives the tube, the reason, and "recollect". The order is not cancelled: it
    /// stays open for the new specimen. OBR-2 is the placer order number, OBR-3 the filler number or the accession.
    /// </summary>
    public static string BuildNoResult(
        Patient patient,
        Order order,
        string testCode,
        string accessionNumber,
        string reason,
        string controlId,
        DateTime nowUtc,
        Hl7OutboundIdentity? identity = null,
        Hl7Encoding? encoding = null,
        Hl7FieldMap? map = null,
        InterfaceValueTranslator? translator = null)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(order);
        var id = identity ?? new Hl7OutboundIdentity();
        map ??= Hl7FieldMap.Default(InterfaceType.Results, Hl7Direction.Outbound);
        translator ??= InterfaceValueTranslator.Empty;
        var text = $"Specimen {accessionNumber} rejected: {reason.Trim().TrimEnd('.')}. Recollect.";

        var builder = new Hl7PathMessageBuilder(encoding)
            .AppendMsh(id.SendingApp, id.SendingFacility, id.ReceivingApp, id.ReceivingFacility, nowUtc, "ORU^R01", controlId)
            .EnsureSegment("PID").EnsureSegment("OBR").EnsureSegment("OBX")
            .Set("PID-1", "1")
            .Set(map.Path(InterfaceDataItemKeys.PatientMrn), translator.ToExternal(InterfaceDataItemKeys.PatientMrn, patient.MedicalRecordNumber))
            .Set(map.Path(InterfaceDataItemKeys.PatientLastName), translator.ToExternal(InterfaceDataItemKeys.PatientLastName, patient.LastName))
            .Set(map.Path(InterfaceDataItemKeys.PatientFirstName), translator.ToExternal(InterfaceDataItemKeys.PatientFirstName, patient.FirstName))
            .Set(map.Path(InterfaceDataItemKeys.PatientDateOfBirth), translator.ToExternal(InterfaceDataItemKeys.PatientDateOfBirth, patient.DateOfBirth.ToString("yyyyMMdd")))
            .Set(map.Path(InterfaceDataItemKeys.PatientSex), translator.ToExternal(InterfaceDataItemKeys.PatientSex, SexCode(patient.Sex)))
            .Set("OBR-1", "1")
            .Set("OBR-2", order.OrderNumber)
            .Set("OBR-3", string.IsNullOrWhiteSpace(order.FillerOrderNumber) ? accessionNumber : order.FillerOrderNumber)
            .Set(map.Path(InterfaceDataItemKeys.ResultObrTestCode), translator.ToExternal(InterfaceDataItemKeys.ResultObrTestCode, testCode))
            .Set("OBR-25", NoResult)
            .Set("OBX-1", "1")
            .Set("OBX-2", "TX")
            .Set(map.Path(InterfaceDataItemKeys.ResultObxIdentifier), translator.ToExternal(InterfaceDataItemKeys.ResultObxIdentifier, testCode))
            .Set(map.Path(InterfaceDataItemKeys.ResultValue), text)
            .Set(map.Path(InterfaceDataItemKeys.ResultObxStatus), translator.ToExternal(InterfaceDataItemKeys.ResultObxStatus, NoResult));

        return builder.Build();
    }

    private static string SexCode(Domain.Enums.Sex sex) => sex switch
    {
        Domain.Enums.Sex.Male => "M",
        Domain.Enums.Sex.Female => "F",
        Domain.Enums.Sex.Other => "O",
        _ => "U"
    };
}
