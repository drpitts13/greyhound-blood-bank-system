using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Tests;

public class Hl7DftBuilderTests
{
    [Fact]
    public void Build_IncludesMshEvnPidFt1_WithBillingCode_AndOmitsAmount()
    {
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-DFT",
            LastName = "Donor",
            FirstName = "Pat",
            MiddleName = "Q",
            DateOfBirth = new DateOnly(1985, 4, 12),
            Sex = Sex.Female
        };
        var billing = new BillingEvent
        {
            Id = 42,
            BillingCode = "BB-ABORH",
            Amount = 35.00m,
            ServiceDateUtc = new DateTime(2026, 8, 23, 15, 30, 0, DateTimeKind.Utc),
            PatientId = 1
        };

        var raw = Hl7DftBuilder.Build(patient, billing, "DFT1", new DateTime(2026, 8, 23, 16, 0, 0, DateTimeKind.Utc));
        var message = Hl7Parser.Parse(raw);

        Assert.Equal("DFT", message.Get("MSH-9-1"));
        Assert.Equal("P03", message.Get("MSH-9-2"));
        Assert.Equal("P03", message.Get("EVN-1"));
        Assert.Equal("MRN-DFT", message.Get("PID-3"));
        Assert.Equal("Donor", message.Get("PID-5-1"));
        Assert.Equal("CG", message.Get("FT1-6"));
        Assert.Equal("BB-ABORH", message.Get("FT1-7-1"));
        Assert.Equal("1", message.Get("FT1-9"));
        Assert.Equal(string.Empty, message.Get("FT1-10"));
        Assert.DoesNotContain("35", raw.Split('\r').First(s => s.StartsWith("FT1", StringComparison.Ordinal)));
    }

    [Fact]
    public void Build_AllowsMissingPatient_AndStillEmitsCharge()
    {
        var billing = new BillingEvent
        {
            BillingCode = "BB-RBC-ISSUE",
            ServiceDateUtc = new DateTime(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc)
        };

        var raw = Hl7DftBuilder.Build(null, billing, "DFT2", new DateTime(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc));
        var message = Hl7Parser.Parse(raw);

        Assert.Equal("BB-RBC-ISSUE", message.Get("FT1-7-1"));
        Assert.Equal(string.Empty, message.Get("PID-3"));
        Assert.Equal(string.Empty, message.Get("FT1-10"));
    }

    [Fact]
    public void Build_HonorsCustomBillingCodePath()
    {
        var billing = new BillingEvent
        {
            BillingCode = "BB-XM",
            ServiceDateUtc = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc)
        };
        var map = Hl7FieldMap.From(InterfaceType.Billing, Hl7Direction.Outbound,
        [
            new InterfaceFieldMapping { DataItemKey = InterfaceDataItemKeys.BillingCode, Hl7Path = "FT1-8" }
        ]);

        var raw = Hl7DftBuilder.Build(null, billing, "DFT3", new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc), map: map);
        var message = Hl7Parser.Parse(raw);

        Assert.Equal("BB-XM", message.Get("FT1-8"));
        Assert.Equal(string.Empty, message.Get("FT1-7"));
    }

    [Fact]
    public void Build_TranslatesInternalBillingCodeToExternal()
    {
        var billing = new BillingEvent
        {
            BillingCode = "BB-XM",
            ServiceDateUtc = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc)
        };
        var translator = InterfaceValueTranslator.From(
        [
            new InterfaceValueTranslation
            {
                DataItemKey = InterfaceDataItemKeys.BillingCode,
                InternalValue = "BB-XM",
                ExternalValue = "71020",
                Direction = InterfaceTranslationDirection.Outbound
            }
        ]);

        var raw = Hl7DftBuilder.Build(null, billing, "DFT4", new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc), translator: translator);
        var message = Hl7Parser.Parse(raw);
        Assert.Equal("71020", message.Get("FT1-7-1"));
    }
}
