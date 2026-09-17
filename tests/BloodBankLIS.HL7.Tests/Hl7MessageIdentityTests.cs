using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Tests;

public class Hl7MessageIdentityTests
{
    [Fact]
    public void TryRead_Orm_SurfacesPlacerAndTest()
    {
        var raw =
            "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ORM^O01|CTRL-O-ERR|P|2.5\r" +
            "PID|1||MISSING-MRN^^^HOSP^MR||Doe^John\r" +
            "ORC|NW|PLACER-999\r" +
            "OBR|1|PLACER-999||TS^Service";

        Assert.True(Hl7MessageIdentity.TryRead(raw, out var info));
        Assert.Equal("MISSING-MRN", info.MedicalRecordNumber);
        Assert.Equal("Doe, John", info.DisplayName);
        Assert.Equal("PLACER-999", info.PlacerOrderNumber);
        Assert.Equal("TS", info.TestCode);
    }

    [Fact]
    public void TryRead_Ras_SurfacesUnitNumber()
    {
        var raw =
            "MSH|^~\\&|EPIC|HOSP|BBLIS|LAB|20260530120000||RAS^O17|CTRL-RAS|P|2.5\r" +
            "PID|1||MRN0009^^^HOSP^MR||Interface^Helen\r" +
            "RXA|0|1|20260530100000|20260530103000|CODE^RBC|300||||12345^Nurse^Pat|||||W000123BPAM001";

        Assert.True(Hl7MessageIdentity.TryRead(raw, out var info));
        Assert.Equal("MRN0009", info.MedicalRecordNumber);
        Assert.Equal("W000123BPAM001", info.UnitNumber);
    }
}
