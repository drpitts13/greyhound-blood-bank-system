using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-336
public class RetrospectiveCrossmatchIncompatibleRuleTests
{
    [Theory]
    [InlineData(UnitStatus.Issued, "may be about to be transfused")]
    [InlineData(UnitStatus.TransfusionStarted, "has started")]
    [InlineData(UnitStatus.Transfused, "has been transfused")]
    public void UnitState_SaysWhereTheUnitStands(UnitStatus status, string expected) =>
        Assert.Contains(expected, RetrospectiveCrossmatchIncompatibleRule.UnitState(status));

    [Fact]
    public void Warning_IsAWarningNamingTheUnit()
    {
        var r = RetrospectiveCrossmatchIncompatibleRule.Warning("W0000 26 123456", UnitStatus.Transfused);
        Assert.Equal(RuleSeverity.Warning, r.Severity);
        Assert.Contains("W0000 26 123456", r.Message);
        Assert.Contains("has been transfused", r.Message);
    }

    [Fact]
    public void Alert_IsCritical_KeyedByIssue_AndNamesThePatient()
    {
        var a = RetrospectiveCrossmatchIncompatibleRule.Alert(42, "MRN9", "Doe, Jane", "W1", UnitStatus.Issued);
        Assert.Equal(AlertSeverity.Critical, a.Severity);
        Assert.Equal("RETROXM:ISSUE-42", a.Key);
        Assert.Equal(AlertRule.RetrospectiveCrossmatchIncompatible, a.Source);
        Assert.Contains("Doe, Jane (MRN MRN9)", a.Message);
    }

    // TEST-BB-390 (OCD-046)
    [Fact]
    public void Deviation_IsCritical_AndItsNumberIsNamedInTheWarningAndAlert()
    {
        Assert.Equal(DeviationSeverity.Critical, RetrospectiveCrossmatchIncompatibleRule.DeviationSeverityLevel);
        var description = RetrospectiveCrossmatchIncompatibleRule.DeviationDescription(42, "MRN9", "Doe, Jane", "W1", UnitStatus.Transfused, 7);
        Assert.Contains("Issue 42", description);
        Assert.Contains("crossmatch 7", description);
        Assert.Contains("has been transfused", description);
        Assert.Contains("Deviation 15 was opened.", RetrospectiveCrossmatchIncompatibleRule.Warning("W1", UnitStatus.Issued, 15).Message);
        Assert.Contains("Deviation 15 was opened.", RetrospectiveCrossmatchIncompatibleRule.Alert(42, "MRN9", null, "W1", UnitStatus.Issued, 15).Message);
        Assert.DoesNotContain("Deviation", RetrospectiveCrossmatchIncompatibleRule.Warning("W1", UnitStatus.Issued).Message);
    }

    [Fact]
    public void Alert_WithoutPatientDetails_SaysNotRecorded() =>
        Assert.Contains("Patient not recorded", RetrospectiveCrossmatchIncompatibleRule.Alert(1, null, " ", "W1", UnitStatus.Issued).Message);
}
