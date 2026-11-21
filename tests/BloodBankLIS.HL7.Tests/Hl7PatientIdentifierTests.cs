using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Tests;

// TEST-BB-568: the MRN is the PID-3 repetition typed MR (the first repetition when none is typed); the other
// repetitions are kept as alternate identifiers; a custom-mapped MRN path is read as configured.
public class Hl7PatientIdentifierTests
{
    private static Hl7Message Pid(string pid3) => Hl7Parser.Parse(
        "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ADT^A08|C1|P|2.5\r" +
        $"PID|1||{pid3}||Smith^Jane||19751203|F");

    [Fact]
    public void Mrn_IsTheMrTypedRepetition_EvenWhenItIsNotFirst()
    {
        var message = Pid("E100200^^^EPI&1.2.3&ISO^PI~MRN555^^^HOSP^MR~SSN9^^^SSA^SS");

        var data = Hl7AdtMapper.Map(message);

        Assert.Equal("MRN555", data.Mrn);
        Assert.Equal(
            [new Hl7PatientIdentifier("E100200", "EPI", "PI"), new Hl7PatientIdentifier("SSN9", "SSA", "SS")],
            data.OtherIdentifiers);
        Assert.Equal("MRN555", Hl7OrmMapper.Map(Hl7Parser.Parse(
            "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ORM^O01|C2|P|2.5\r" +
            "PID|1||E100200^^^EPI^PI~MRN555^^^HOSP^MR||Smith^Jane\r" +
            "ORC|NW|P1\rOBR|1|P1||ABORH")).Mrn);
    }

    [Fact]
    public void Mrn_WithoutATypedRepetition_IsTheFirst_AsBefore()
    {
        var data = Hl7AdtMapper.Map(Pid("MRN777~OTHER1"));
        Assert.Equal("MRN777", data.Mrn);
        Assert.Equal([new Hl7PatientIdentifier("OTHER1", null, null)], data.OtherIdentifiers);

        var single = Hl7AdtMapper.Map(Pid("MRN777^^^HOSP^MR"));
        Assert.Empty(single.OtherIdentifiers!);
    }

    [Fact]
    public void CustomMrnPath_IsReadAsConfigured()
    {
        var map = Hl7FieldMap.From(InterfaceType.Adt, Hl7Direction.Inbound,
            [new InterfaceFieldMapping { DataItemKey = InterfaceDataItemKeys.PatientMrn, Hl7Path = "PID-2" }]);
        var message = Hl7Parser.Parse(
            "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ADT^A08|C1|P|2.5\r" +
            "PID|1|EXT42|E1^^^EPI^PI~MRN555^^^HOSP^MR||Smith^Jane");

        var data = Hl7AdtMapper.Map(message, map);

        Assert.Equal("EXT42", data.Mrn);
        Assert.Empty(data.OtherIdentifiers!);
    }
}
