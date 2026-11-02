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

    [Fact]
    public void Alert_WithoutPatientDetails_SaysNotRecorded() =>
        Assert.Contains("Patient not recorded", RetrospectiveCrossmatchIncompatibleRule.Alert(1, null, " ", "W1", UnitStatus.Issued).Message);
}
