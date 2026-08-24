using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
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
        Hl7Encoding? encoding = null,
        Hl7FieldMap? map = null,
        InterfaceValueTranslator? translator = null)
    {
        ArgumentNullException.ThrowIfNull(billingEvent);
        var id = identity ?? new Hl7OutboundIdentity();
        map ??= Hl7FieldMap.Default(InterfaceType.Billing, Hl7Direction.Outbound);
        translator ??= InterfaceValueTranslator.Empty;

        var builder = new Hl7PathMessageBuilder(encoding)
            .AppendMsh(id.SendingApp, id.SendingFacility, id.ReceivingApp, id.ReceivingFacility, nowUtc, "DFT^P03", controlId)
            .EnsureSegment("EVN").EnsureSegment("PID").EnsureSegment("FT1")
            .Set("EVN-1", "P03")
            .Set("EVN-2", nowUtc.ToString(Hl7MessageBuilder.Hl7TimestampFormat))
            .Set("PID-1", "1")
            .Set(map.Path(InterfaceDataItemKeys.PatientMrn), translator.ToExternal(InterfaceDataItemKeys.PatientMrn, patient?.MedicalRecordNumber))
            .Set(map.Path(InterfaceDataItemKeys.PatientLastName), translator.ToExternal(InterfaceDataItemKeys.PatientLastName, patient?.LastName))
            .Set(map.Path(InterfaceDataItemKeys.PatientFirstName), translator.ToExternal(InterfaceDataItemKeys.PatientFirstName, patient?.FirstName))
            .Set(map.Path(InterfaceDataItemKeys.PatientMiddleName), translator.ToExternal(InterfaceDataItemKeys.PatientMiddleName, patient?.MiddleName))
            .Set(map.Path(InterfaceDataItemKeys.PatientDateOfBirth), translator.ToExternal(InterfaceDataItemKeys.PatientDateOfBirth, patient is null ? null : patient.DateOfBirth.ToString("yyyyMMdd")))
            .Set(map.Path(InterfaceDataItemKeys.PatientSex), translator.ToExternal(InterfaceDataItemKeys.PatientSex, SexCode(patient)))
            .Set("FT1-1", "1")
            .Set("FT1-2", billingEvent.Id > 0 ? billingEvent.Id.ToString() : string.Empty)
            .Set(map.Path(InterfaceDataItemKeys.BillingServiceDate), translator.ToExternal(InterfaceDataItemKeys.BillingServiceDate, billingEvent.ServiceDateUtc.ToString(Hl7MessageBuilder.Hl7TimestampFormat)))
            .Set(map.Path(InterfaceDataItemKeys.BillingTransactionType), translator.ToExternal(InterfaceDataItemKeys.BillingTransactionType, "CG"))
            .Set(map.Path(InterfaceDataItemKeys.BillingCode), translator.ToExternal(InterfaceDataItemKeys.BillingCode, billingEvent.BillingCode))
            .Set(map.Path(InterfaceDataItemKeys.BillingQuantity), translator.ToExternal(InterfaceDataItemKeys.BillingQuantity, "1"));

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
