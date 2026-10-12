using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ShiftHandoffRuleTests
{
    [Fact]
    public void Permission_WithoutAuditRead_IsHardStop()
    {
        var result = ShiftHandoffRule.EvaluatePermission(hasAuditRead: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ShiftHandoffRule.PermissionCode, result.Code);
    }

    [Fact]
    public void Notes_Blank_IsHardStop()
    {
        var result = ShiftHandoffRule.EvaluateNotes("   ");
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ShiftHandoffRule.NotesCode, result.Code);
    }

    [Fact]
    public void Acknowledge_SameUser_IsHardStop()
    {
        var result = ShiftHandoffRule.EvaluateAcknowledge("tech1", "TECH1", acknowledgedUtc: null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ShiftHandoffRule.SelfAckCode, result.Code);
    }

    [Fact]
    public void Acknowledge_AlreadyAcknowledged_IsHardStop()
    {
        var result = ShiftHandoffRule.EvaluateAcknowledge(
            "tech1", "tech2", acknowledgedUtc: DateTime.UtcNow);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ShiftHandoffRule.AlreadyAckCode, result.Code);
    }

    [Fact]
    public void Acknowledge_DifferentUser_Passes()
    {
        var result = ShiftHandoffRule.EvaluateAcknowledge("tech1", "tech2", acknowledgedUtc: null);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
