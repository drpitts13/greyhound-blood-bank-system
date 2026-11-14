using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-363: a crossmatch is not recorded on an expired specimen, and an expired crossmatch is offered for repeat.
public class CrossmatchSpecimenRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ExpiredSpecimen_IsAHardStop_NamingTheSpecimenAndTheNextStep()
    {
        var result = CrossmatchSpecimenRule.Evaluate("ACC-1", Now.AddMinutes(-1), Now);

        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(CrossmatchSpecimenRule.Code, result.Code);
        Assert.Contains("Specimen ACC-1 expired at 2026-11-02 11:59 UTC", result.Message);
        Assert.Contains("Collect a new specimen", result.Message);
    }

    [Fact]
    public void AtTheExpirationTime_TheSpecimenHasExpired()
    {
        Assert.Equal(RuleSeverity.HardStop, CrossmatchSpecimenRule.Evaluate(null, Now, Now).Severity);
        Assert.StartsWith("The specimen expired", CrossmatchSpecimenRule.Evaluate(null, Now, Now).Message);
    }

    [Fact]
    public void InDateOrUndatedSpecimen_Passes()
    {
        Assert.Equal(RuleSeverity.Pass, CrossmatchSpecimenRule.Evaluate("ACC-1", Now.AddMinutes(1), Now).Severity);
        Assert.Equal(RuleSeverity.Pass, CrossmatchSpecimenRule.Evaluate("ACC-1", null, Now).Severity);
    }

    // TEST-BB-364 (OCD-050): on the pre-transfusion specimen, the retrospective crossmatch is an exception, not a hard stop.
    [Fact]
    public void RetrospectiveOnPreTransfusionSpecimen_IsAWarningToOverride()
    {
        var result = CrossmatchSpecimenRule.Evaluate("ACC-1", Now.AddMinutes(-1), Now, retrospectiveOnPreTransfusionSpecimen: true);

        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(CrossmatchSpecimenRule.RetrospectiveCode, result.Code);
        Assert.Contains("pre-transfusion specimen", result.Message);
        Assert.Equal(RuleSeverity.Pass, CrossmatchSpecimenRule.Evaluate("ACC-1", Now.AddMinutes(1), Now, retrospectiveOnPreTransfusionSpecimen: true).Severity);
    }

    [Fact]
    public void PreTransfusionSpecimen_IsCollectedAtOrBeforeIssue()
    {
        Assert.True(CrossmatchSpecimenRule.IsPreTransfusionSpecimen(Now.AddHours(-2), Now));
        Assert.True(CrossmatchSpecimenRule.IsPreTransfusionSpecimen(Now, Now));
        Assert.False(CrossmatchSpecimenRule.IsPreTransfusionSpecimen(Now.AddMinutes(1), Now));
    }

    [Fact]
    public void ExpiredCompatibleCrossmatch_NeedsRepeating_UnlessElectronicOrIssued()
    {
        Assert.True(CrossmatchEntryNeed.ForAllocation(true, CrossmatchResult.Compatible, "XM", AllocationStatus.Reserved, null, compatibleCrossmatchExpired: true));
        Assert.False(CrossmatchEntryNeed.ForAllocation(true, CrossmatchResult.Compatible, "XM", AllocationStatus.Reserved, null, compatibleCrossmatchExpired: false));
        Assert.False(CrossmatchEntryNeed.ForAllocation(true, CrossmatchResult.Compatible, "EXM", AllocationStatus.Reserved, null, compatibleCrossmatchExpired: true));
        Assert.False(CrossmatchEntryNeed.ForAllocation(true, CrossmatchResult.Compatible, "XM", AllocationStatus.Reserved, 7, compatibleCrossmatchExpired: true));
        Assert.False(CrossmatchEntryNeed.ForAllocation(true, CrossmatchResult.Incompatible, "XM", AllocationStatus.Reserved, null, compatibleCrossmatchExpired: true));
    }
}
