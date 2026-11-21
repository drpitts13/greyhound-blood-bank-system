using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Tests;

public class Hl7BpsBuilderTests
{
    private static readonly DateTime Issued = new(2026, 11, 4, 14, 5, 0, DateTimeKind.Utc);

    private static (Patient, Issue, BloodUnit, ProductType, Order) Scenario() => (
        new Patient { MedicalRecordNumber = "MRN-BPS", LastName = "Bedside", FirstName = "Pat", DateOfBirth = new DateOnly(1970, 3, 9) },
        new Issue { Id = 77, PatientId = 1, BloodProductId = 5, IssuedUtc = Issued, IssuedToLocation = "ICU", IssuedTo = "RN Lee", IssuedBy = "tech1" },
        new BloodUnit { Id = 5, UnitNumber = "W123426000777", Din = "W123426000777", AboRhdCode = "5100", Abo = AboGroup.O, RhD = RhType.Positive },
        new ProductType { ProductCode = "E0382", Name = "Red Blood Cells, Leukoreduced" },
        new Order { OrderNumber = "EHR-12345", FillerOrderNumber = "BB-98" });

    [Fact]
    public void Dispensed_SendsBpsO29_WithTheUnitTheOrderAndWhereItWent()
    {
        var (patient, issue, unit, product, order) = Scenario();

        var raw = Hl7BpsBuilder.Build(patient, issue, unit, product, order, ProductDispenseEvent.Dispensed, Issued, "BPS1", Issued.AddSeconds(5));
        var message = Hl7Parser.Parse(raw);

        Assert.Equal("BPS", message.Get("MSH-9-1"));
        Assert.Equal("O29", message.Get("MSH-9-2"));
        Assert.Equal("MRN-BPS", message.Get("PID-3"));
        Assert.Equal("ICU", message.Get("PV1-3-1"));
        Assert.Equal("SC", message.Get("ORC-1"));
        Assert.Equal("EHR-12345", message.Get("ORC-2"));
        Assert.Equal("BB-98", message.Get("ORC-3"));
        Assert.Equal("E0382", message.Get("BPO-2-1"));
        Assert.Equal("DS", message.Get("BPX-2-1"));
        Assert.Equal("HL70510", message.Get("BPX-2-3"));
        Assert.Equal("F", message.Get("BPX-3"));
        Assert.Equal("20261104140500", message.Get("BPX-4")[..14]);
        Assert.Equal("W123426000777", message.Get("BPX-5"));
        Assert.Equal("E0382", message.Get("BPX-6-1"));
        Assert.Equal("5100", message.Get("BPX-11-1"));
        Assert.Equal("ICU", message.Get("BPX-18-1"));
        Assert.Equal("RN Lee", message.Get("BPX-20-2"));
    }

    [Fact]
    public void Returned_SendsReturnedUnused_WithoutADestination_AndWithoutAnOrderUsesTheIssue()
    {
        var (patient, issue, unit, product, _) = Scenario();
        var returnedAt = Issued.AddMinutes(25);

        var raw = Hl7BpsBuilder.Build(patient, issue, unit, product, order: null, ProductDispenseEvent.Returned, returnedAt, "BPS2", returnedAt);
        var message = Hl7Parser.Parse(raw);

        Assert.Equal("RA", message.Get("BPX-2-1"));
        Assert.Equal("20261104143000", message.Get("BPX-4")[..14]);
        Assert.Equal(string.Empty, message.Get("BPX-18"));
        Assert.Equal(string.Empty, message.Get("ORC-2"));
        Assert.Equal("ISS77", message.Get("ORC-3"));
    }

    [Fact]
    public void TheDispenseStatus_CanBeTranslatedForASite()
    {
        var (patient, issue, unit, product, order) = Scenario();
        var translator = InterfaceValueTranslator.From([
            new InterfaceValueTranslation { DataItemKey = Hl7BpsBuilder.StatusKey, InternalValue = "DS", ExternalValue = "DISPENSED" }
        ]);

        var raw = Hl7BpsBuilder.Build(patient, issue, unit, product, order, ProductDispenseEvent.Dispensed, Issued, "BPS3", Issued, translator: translator);

        Assert.Equal("DISPENSED", Hl7Parser.Parse(raw).Get("BPX-2-1"));
    }
}
