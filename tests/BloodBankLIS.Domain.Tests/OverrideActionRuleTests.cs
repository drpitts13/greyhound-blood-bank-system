using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-338
public class OverrideActionRuleTests
{
    [Theory]
    [InlineData(IssueType.EmergencyRelease, OverrideAction.EmergencyRelease)]
    [InlineData(IssueType.MassiveTransfusion, OverrideAction.MassiveTransfusion)]
    [InlineData(IssueType.Standard, OverrideAction.WarningOverride)]
    public void ForIssue_RecordsWhatTheIssueWas(IssueType issueType, OverrideAction expected) =>
        Assert.Equal(expected, OverrideActionRule.ForIssue(issueType));

    [Theory]
    [InlineData(OverrideAction.EmergencyRelease, "Emergency release")]
    [InlineData(OverrideAction.MassiveTransfusion, "Massive transfusion release")]
    [InlineData(OverrideAction.WarningOverride, "Warning override")]
    public void Describe_NamesEachAction(OverrideAction action, string expected) =>
        Assert.Equal(expected, OverrideActionRule.Describe(action));
}
