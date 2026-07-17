using BloodBankLIS.Domain.Entities;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Messaging;

/// <summary>Facility identifiers used when composing outbound messages.</summary>
public sealed record Hl7OutboundIdentity(
    string SendingApp = "BloodBankLIS",
    string SendingFacility = "BBLIS",
    string ReceivingApp = "EHR",
    string ReceivingFacility = "HOSP");

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
        Hl7Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(result);
        var id = identity ?? new Hl7OutboundIdentity();

        var builder = new Hl7MessageBuilder(encoding)
            .AppendMsh(id.SendingApp, id.SendingFacility, id.ReceivingApp, id.ReceivingFacility, nowUtc, "ORU^R01", controlId);

        // PID-3 MRN, PID-5 name, PID-7 DOB, PID-8 sex.
        builder.AppendSegment("PID",
            "1",
            string.Empty,
            builder.Field(patient.MedicalRecordNumber),
            string.Empty,
            builder.Field(patient.LastName, patient.FirstName, patient.MiddleName),
            string.Empty,
            patient.DateOfBirth.ToString("yyyyMMdd"),
            SexCode(patient));

        builder.AppendSegment("OBR",
            "1",
            string.Empty,
            string.Empty,
            builder.Field(result.TestCode),
            string.Empty,
            string.Empty,
            (result.VerifiedUtc ?? nowUtc).ToString(Hl7MessageBuilder.Hl7TimestampFormat));

        builder.AppendSegment("OBX",
            "1",
            "ST",
            builder.Field(result.TestCode),
            string.Empty,
            builder.Field(result.Value),
            builder.Field(result.Units),
            string.Empty,
            builder.Field(result.Interpretation),
            string.Empty,
            string.Empty,
            "F"); // result status: Final

        return builder.Build();
    }

    private static string SexCode(Patient patient) => patient.Sex switch
    {
        Domain.Enums.Sex.Male => "M",
        Domain.Enums.Sex.Female => "F",
        Domain.Enums.Sex.Other => "O",
        _ => "U"
    };
}
