using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Tests;

public class Hl7MapperTests
{
    [Fact]
    public void AdtMapper_ExtractsDemographics()
    {
        var message = Hl7Parser.Parse(
            "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ADT^A08|C1|P|2.5\r" +
            "PID|1||MRN777^^^HOSP^MR||Smith^Jane^M||19751203|F");

        var data = Hl7AdtMapper.Map(message);

        Assert.Equal("MRN777", data.Mrn);
        Assert.Equal("Smith", data.LastName);
        Assert.Equal("Jane", data.FirstName);
        Assert.Equal("M", data.MiddleName);
        Assert.Equal(new DateOnly(1975, 12, 3), data.DateOfBirth);
        Assert.Equal(Sex.Female, data.Sex);
    }

    [Theory]
    [InlineData("M", Sex.Male)]
    [InlineData("F", Sex.Female)]
    [InlineData("O", Sex.Other)]
    [InlineData("U", Sex.Unknown)]
    [InlineData("", Sex.Unknown)]
    public void AdtMapper_MapsSexCodes(string code, Sex expected)
    {
        Assert.Equal(expected, Hl7AdtMapper.MapSex(code));
    }

    [Fact]
    public void OrmMapper_ExtractsOrderControlPlacerIdAndType()
    {
        var message = Hl7Parser.Parse(
            "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ORM^O01|C2|P|2.5\r" +
            "PID|1||MRN777^^^HOSP^MR||Smith^Jane\r" +
            "ORC|NW|PLACER-100\r" +
            "OBR|1|PLACER-100||TS^Type and Screen");

        var data = Hl7OrmMapper.Map(message);

        Assert.Equal("NW", data.OrderControl);
        Assert.Equal("PLACER-100", data.PlacerOrderId);
        Assert.Equal("MRN777", data.Mrn);
        Assert.Equal(OrderType.TypeAndScreen, data.OrderType);
        Assert.Null(data.OrderingProviderId);
    }

    [Theory]
    [InlineData("XM", OrderType.Crossmatch)]
    [InlineData("T&S", OrderType.TypeAndScreen)]
    [InlineData("UNKNOWNCODE", OrderType.Other)]
    public void OrmMapper_MapsOrderTypes(string code, OrderType expected)
    {
        Assert.Equal(expected, Hl7OrmMapper.MapOrderType(code));
    }

    [Fact]
    public void AdtMapper_UsesCustomMrnPath()
    {
        var message = Hl7Parser.Parse(
            "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ADT^A08|C3|P|2.5\r" +
            "PID|1|ALT-MRN|IGNORED^^^HOSP^MR||Lee^Sam");

        var map = Hl7FieldMap.From(InterfaceType.Adt, Hl7Direction.Inbound,
        [
            new InterfaceFieldMapping { DataItemKey = InterfaceDataItemKeys.PatientMrn, Hl7Path = "PID-2" }
        ]);

        var data = Hl7AdtMapper.Map(message, map);
        Assert.Equal("ALT-MRN", data.Mrn);
        Assert.Equal("Lee", data.LastName);
    }

    [Fact]
    public void BpamMapper_ExtractsUnitAndVolume()
    {
        var message = Hl7Parser.Parse(
            "MSH|^~\\&|EPIC|HOSP|BBLIS|LAB|20260530120000||RAS^O17|C4|P|2.5\r" +
            "PID|1||MRN900^^^HOSP^MR||Blood^Pat\r" +
            "RXA|0|1|20260530100000|20260530103000|CODE^RBC|350||||12345^Nurse^Pat|||||W0001-00");

        var data = Hl7BpamMapper.Map(message);
        Assert.Equal("MRN900", data.Mrn);
        Assert.Equal("W0001-00", data.UnitNumber);
        Assert.Equal(350m, data.VolumeTransfused);
    }
}
