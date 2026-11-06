using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.HL7.Messaging;

namespace BloodBankLIS.HL7.Tests;

// TEST-BB-357: OBX-11 tells the EHR whether a result is final, a correction, or withdrawn as wrong.
public class Hl7OruBuilderTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private static string ObxStatus(TestResult result)
    {
        var patient = new Patient { MedicalRecordNumber = "MRN1", LastName = "Doe", FirstName = "Jane", DateOfBirth = new DateOnly(1980, 1, 1), Sex = Sex.Female };
        var raw = Hl7OruBuilder.Build(patient, result, "CTRL1", Now);
        var obx = raw.Split('\r', StringSplitOptions.RemoveEmptyEntries).Single(s => s.StartsWith("OBX|", StringComparison.Ordinal));
        return obx.Split('|')[11];
    }

    [Fact]
    public void FirstVerifiedResult_IsFinal() =>
        Assert.Equal("F", ObxStatus(new TestResult { TestCode = "ABORH", Value = "A Positive", Status = ResultStatus.Verified, Version = 1, VerifiedUtc = Now }));

    [Fact]
    public void VerifiedCorrection_IsCorrected() =>
        Assert.Equal("C", ObxStatus(new TestResult { TestCode = "ABORH", Value = "O Positive", Status = ResultStatus.Verified, Version = 2, VerifiedUtc = Now }));

    [Fact]
    public void InvalidatedAfterVerification_IsPostedAsWrong() =>
        Assert.Equal("W", ObxStatus(new TestResult { TestCode = "ABORH", Value = "A Positive", Status = ResultStatus.Invalidated, Version = 2, VerifiedUtc = Now }));

    // TEST-BB-498 (OCD-062): a rejected specimen's test goes to the EHR as "no result": OBR-25 X, OBX-11 X, the order
    // numbers, and the reason with "recollect".
    [Fact]
    public void NoResultNotice_CarriesTheOrderAndTheReason()
    {
        var patient = new Patient { MedicalRecordNumber = "MRN1", LastName = "Doe", FirstName = "Jane", DateOfBirth = new DateOnly(1980, 1, 1), Sex = Sex.Female };
        var order = new Order { OrderNumber = "ORD77" };
        var raw = Hl7OruBuilder.BuildNoResult(patient, order, "ABSC", "ACC9", "Hemolyzed.", "CTRL9", Now);
        var segments = raw.Split('\r', StringSplitOptions.RemoveEmptyEntries);
        var obr = segments.Single(s => s.StartsWith("OBR|", StringComparison.Ordinal)).Split('|');
        var obx = segments.Single(s => s.StartsWith("OBX|", StringComparison.Ordinal)).Split('|');

        Assert.StartsWith("MSH|", segments[0]);
        Assert.Contains("ORU^R01", segments[0]);
        Assert.Equal("ORD77", obr[2]);
        Assert.Equal("ACC9", obr[3]);
        Assert.Equal("X", obr[25]);
        Assert.Equal("X", obx[11]);
        Assert.Equal("Specimen ACC9 rejected: Hemolyzed. Recollect.", obx[5]);
    }
}
