using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
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
/// Builds an outbound ORU^R01 result message (MSH + PID + OBR + OBX) from a verified
/// test result, triggered when a result is verified (docs/hl7-design.md section 2.3).
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
            .Set("OBX-2", "ST")
            .Set(map.Path(InterfaceDataItemKeys.ResultObxIdentifier), translator.ToExternal(InterfaceDataItemKeys.ResultObxIdentifier, result.TestCode))
            .Set(map.Path(InterfaceDataItemKeys.ResultValue), translator.ToExternal(InterfaceDataItemKeys.ResultValue, result.Value))
            .Set(map.Path(InterfaceDataItemKeys.ResultUnits), translator.ToExternal(InterfaceDataItemKeys.ResultUnits, result.Units))
            .Set(map.Path(InterfaceDataItemKeys.ResultInterpretation), translator.ToExternal(InterfaceDataItemKeys.ResultInterpretation, result.Interpretation))
            .Set(map.Path(InterfaceDataItemKeys.ResultObxStatus), translator.ToExternal(InterfaceDataItemKeys.ResultObxStatus, "F"));

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
