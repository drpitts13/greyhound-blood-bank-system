using BloodBankLIS.Domain.Entities;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Messaging;

/// <summary>
/// Builds a standard outbound DFT^P03 charge message (MSH + EVN + PID + FT1).
/// Price is intentionally omitted from FT1 (internal tracking only).
/// </summary>
public static class Hl7DftBuilder
{
    public static string Build(
        Patient? patient,
        BillingEvent billingEvent,
        string controlId,
        DateTime nowUtc,
        Hl7OutboundIdentity? identity = null,
        Hl7Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(billingEvent);
        var id = identity ?? new Hl7OutboundIdentity();

        var builder = new Hl7MessageBuilder(encoding)
            .AppendMsh(id.SendingApp, id.SendingFacility, id.ReceivingApp, id.ReceivingFacility, nowUtc, "DFT^P03", controlId);

        builder.AppendSegment("EVN",
            "P03",
            nowUtc.ToString(Hl7MessageBuilder.Hl7TimestampFormat));

        builder.AppendSegment("PID",
            "1",
            string.Empty,
            builder.Field(patient?.MedicalRecordNumber),
            string.Empty,
            builder.Field(patient?.LastName, patient?.FirstName, patient?.MiddleName),
            string.Empty,
            patient is null ? string.Empty : patient.DateOfBirth.ToString("yyyyMMdd"),
            SexCode(patient));

        var serviceDate = billingEvent.ServiceDateUtc.ToString(Hl7MessageBuilder.Hl7TimestampFormat);
        builder.AppendSegment("FT1",
            "1",
            billingEvent.Id > 0 ? billingEvent.Id.ToString() : string.Empty,
            string.Empty,
            serviceDate,
            string.Empty,
            "CG",
            builder.Field(billingEvent.BillingCode),
            string.Empty,
            "1",
            string.Empty);

        return builder.Build();
    }

    private static string SexCode(Patient? patient) => patient?.Sex switch
    {
        Domain.Enums.Sex.Male => "M",
        Domain.Enums.Sex.Female => "F",
        Domain.Enums.Sex.Other => "O",
        Domain.Enums.Sex.Unknown => "U",
        _ => string.Empty
    };
}
