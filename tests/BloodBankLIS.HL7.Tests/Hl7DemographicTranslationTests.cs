using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Tests;

// TEST-BB-527: race (PID-10), ethnicity (PID-22), and patient class (PV1-2) are read through the translations into
// local values, an untranslatable value is listed and not stored; they go out translated on results, charges, and
// dispense messages; OBX-8 carries the translated abnormal flag, else the translated interpretation.
public class Hl7DemographicTranslationTests
{
    private static InterfaceValueTranslation Row(string key, string local, string external) =>
        new() { DataItemKey = key, InternalValue = local, ExternalValue = external, Direction = InterfaceTranslationDirection.Both };

    private static InterfaceValueTranslator Translator() => InterfaceValueTranslator.From(
    [
        Row(InterfaceDataItemKeys.PatientRace, "WHITE", "2106-3"),
        Row(InterfaceDataItemKeys.PatientEthnicity, "NOT-HISPANIC", "2186-5"),
        Row(InterfaceDataItemKeys.EncounterPatientClass, "Emergency", "E"),
        Row(InterfaceDataItemKeys.ResultAbnormalFlag, "HH", "AA")
    ]);

    private static Patient Patient() => new()
    {
        Id = 1, MedicalRecordNumber = "MRN-D", LastName = "Demo", FirstName = "Pat", DateOfBirth = new DateOnly(1980, 1, 1),
        Sex = Sex.Female, Race = "WHITE", Ethnicity = "NOT-HISPANIC"
    };

    [Fact]
    public void Adt_ReadsRaceEthnicityAndPatientClass_ThroughTheTranslations()
    {
        var message = Hl7Parser.Parse(
            "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20261107120000||ADT^A04|D1|P|2.5\r" +
            "PID|1||MRN-D^^^HOSP^MR||Demo^Pat||19800101|F||2106-3^White^CDCREC||||||||||||2186-5^Not Hispanic^CDCREC\r" +
            "PV1|1|E|ED^1");
        var map = Hl7FieldMap.Default(InterfaceType.Adt, Hl7Direction.Inbound);
        map.Translator = Translator();

        var data = Hl7AdtMapper.Map(message, map);

        Assert.Equal(("WHITE", "NOT-HISPANIC", (EncounterType?)EncounterType.Emergency), (data.Race, data.Ethnicity, data.PatientClass));
        Assert.Empty(map.Translator.Misses);
    }

    [Fact]
    public void Adt_UntranslatedValues_AreListed_AndNotStored()
    {
        var message = Hl7Parser.Parse(
            "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20261107120000||ADT^A08|D2|P|2.5\r" +
            "PID|1||MRN-D^^^HOSP^MR||Demo^Pat||19800101|F||9999-9^Unknown code\r" +
            "PV1|1|Z");
        var map = Hl7FieldMap.Default(InterfaceType.Adt, Hl7Direction.Inbound);
        map.Translator = Translator();

        var data = Hl7AdtMapper.Map(message, map);

        Assert.Null(data.Race);
        Assert.Null(data.Ethnicity);
        Assert.Null(data.PatientClass);
        Assert.Contains(map.Translator.Misses, m => m.DataItemKey == InterfaceDataItemKeys.PatientRace && m.Value == "9999-9");
        Assert.Contains(map.Translator.Misses, m => m.DataItemKey == InterfaceDataItemKeys.EncounterPatientClass && m.Value == "Z");
    }

    [Fact]
    public void Outbound_SendsTranslatedRaceEthnicity_FlagAndPatientClass()
    {
        var translator = Translator();
        var result = new TestResult { Id = 5, PatientId = 1, TestCode = "FMHQ", Value = "6.1", Units = "%", AbnormalFlag = "HH", Interpretation = "High", Status = ResultStatus.Verified, VerifiedUtc = DateTime.UtcNow };
        var oru = Hl7Parser.Parse(Hl7OruBuilder.Build(Patient(), result, "O1", DateTime.UtcNow, translator: translator));
        Assert.Equal(("2106-3", "2186-5", "AA"), (oru.Get("PID-10-1"), oru.Get("PID-22-1"), oru.Get("OBX-8")));

        var interpretationOnly = Hl7Parser.Parse(Hl7OruBuilder.Build(Patient(), new TestResult
        {
            Id = 6, PatientId = 1, TestCode = "DAT", Value = "Positive", Interpretation = "Positive", Status = ResultStatus.Verified, VerifiedUtc = DateTime.UtcNow
        }, "O2", DateTime.UtcNow, translator: translator));
        Assert.Equal("Positive", interpretationOnly.Get("OBX-8"));

        var charge = Hl7Parser.Parse(Hl7DftBuilder.Build(Patient(),
            new BillingEvent { PatientId = 1, BillingCode = "BB-XM", ServiceDateUtc = DateTime.UtcNow }, "F1", DateTime.UtcNow, translator: translator));
        Assert.Equal("2106-3", charge.Get("PID-10-1"));

        var unit = new BloodUnit { Id = 3, UnitNumber = "W000126000001", ProductTypeId = 1, Abo = AboGroup.O, RhD = RhType.Negative, ExpiresUtc = DateTime.UtcNow.AddDays(10) };
        var issue = new Issue { Id = 4, PatientId = 1, BloodProductId = 3, IssuedUtc = DateTime.UtcNow, IssuedToLocation = "ED" };
        var emergency = Hl7Parser.Parse(Hl7BpsBuilder.Build(Patient(), issue, unit, null, null, ProductDispenseEvent.Dispensed, DateTime.UtcNow, "B1", DateTime.UtcNow,
            translator: translator, visitType: EncounterType.Emergency));
        Assert.Equal(("E", "2106-3"), (emergency.Get("PV1-2"), emergency.Get("PID-10-1")));
        var noVisit = Hl7Parser.Parse(Hl7BpsBuilder.Build(Patient(), issue, unit, null, null, ProductDispenseEvent.Dispensed, DateTime.UtcNow, "B2", DateTime.UtcNow,
            translator: translator));
        Assert.Equal("U", noVisit.Get("PV1-2"));
    }
}
