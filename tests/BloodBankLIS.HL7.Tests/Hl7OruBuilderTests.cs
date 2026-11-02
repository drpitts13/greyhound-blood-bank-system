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
}
