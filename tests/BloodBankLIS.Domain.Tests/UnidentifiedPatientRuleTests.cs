using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-590: the temporary MRN and name carry the date and the day's number; the estimated birth date gives the age;
// sex and age are required and bounded; identifying needs an unidentified, unmerged chart and a different identified one.
public class UnidentifiedPatientRuleTests
{
    [Fact]
    public void TemporaryIdentity_CarriesTheDateAndNumber()
    {
        var at = new DateTime(2026, 11, 8, 23, 30, 0, DateTimeKind.Utc);
        Assert.Equal("UNK-20261108-03", UnidentifiedPatientRule.Mrn(at, 3));
        Assert.Equal("UNK-20261108-", UnidentifiedPatientRule.DayPrefix(at));
        Assert.Equal("TRAUMA 20261108-03", UnidentifiedPatientRule.FirstName(at, 3));
        Assert.Equal(new DateOnly(1996, 1, 1), UnidentifiedPatientRule.EstimatedBirthDate(new DateOnly(2026, 11, 8), 30));
        Assert.Equal(new DateOnly(2026, 1, 1), UnidentifiedPatientRule.EstimatedBirthDate(new DateOnly(2026, 11, 8), 0));
    }

    [Fact]
    public void Evaluate_RequiresSexAndAge_AndIdentifyNeedsADifferentIdentifiedChart()
    {
        Assert.Equal(RuleSeverity.Pass, UnidentifiedPatientRule.Evaluate(Sex.Unknown, 0).Severity);
        Assert.Equal(RuleSeverity.HardStop, UnidentifiedPatientRule.Evaluate(null, 30).Severity);
        Assert.Equal(RuleSeverity.HardStop, UnidentifiedPatientRule.Evaluate(Sex.Male, null).Severity);
        Assert.Equal(RuleSeverity.HardStop, UnidentifiedPatientRule.Evaluate(Sex.Male, -1).Severity);

        Assert.Equal(RuleSeverity.Pass, UnidentifiedPatientRule.EvaluateIdentify(true, PatientStatus.Active, 1, 2, false, PatientStatus.Active).Severity);
        Assert.Contains("not an unidentified", UnidentifiedPatientRule.EvaluateIdentify(false, PatientStatus.Active, 1, 2, false, PatientStatus.Active).Message);
        Assert.Contains("already merged", UnidentifiedPatientRule.EvaluateIdentify(true, PatientStatus.Merged, 1, 2, false, PatientStatus.Active).Message);
        Assert.Contains("No patient", UnidentifiedPatientRule.EvaluateIdentify(true, PatientStatus.Active, 1, null, false, null).Message);
        Assert.Contains("not this chart's", UnidentifiedPatientRule.EvaluateIdentify(true, PatientStatus.Active, 1, 1, true, PatientStatus.Active).Message);
        Assert.Contains("another unidentified", UnidentifiedPatientRule.EvaluateIdentify(true, PatientStatus.Active, 1, 2, true, PatientStatus.Active).Message);
        Assert.Contains("merged into another", UnidentifiedPatientRule.EvaluateIdentify(true, PatientStatus.Active, 1, 2, false, PatientStatus.Merged).Message);
    }
}
