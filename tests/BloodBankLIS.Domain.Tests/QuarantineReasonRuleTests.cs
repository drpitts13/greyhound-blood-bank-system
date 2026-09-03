using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class QuarantineReasonRuleTests
{
    [Fact]
    public void Unspecified_IsHardStop()
    {
        var result = QuarantineReasonRule.Evaluate(UnitQuarantineReason.Unspecified, null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(QuarantineReasonRule.Code, result.Code);
    }

    [Fact]
    public void OtherWithoutNotes_IsHardStop()
    {
        var result = QuarantineReasonRule.Evaluate(UnitQuarantineReason.Other, "  ");
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(QuarantineReasonRule.Code, result.Code);
    }

    [Theory]
    [InlineData(UnitQuarantineReason.PendingRelease, null)]
    [InlineData(UnitQuarantineReason.VisualDefect, "clots")]
    [InlineData(UnitQuarantineReason.Other, "Supervisor directed hold")]
    public void CodedReason_Passes(UnitQuarantineReason reason, string? notes)
    {
        var result = QuarantineReasonRule.Evaluate(reason, notes);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
