using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ShiftHandoffRuleTests
{
    [Fact]
    public void Permission_WithoutShiftHandoff_IsHardStop()
    {
        var result = ShiftHandoffRule.EvaluatePermission(hasShiftHandoff: false);
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

    // TEST-BB-330
    [Theory]
    [InlineData(null, true, false)]
    [InlineData("  ", false, false)]
    [InlineData("tech2", true, false)]
    [InlineData("tech2", false, true)]
    [InlineData("TECH1", true, true)]
    public void Recipient_MustBeAnotherActiveUser(string? toUser, bool active, bool stopped) =>
        Assert.Equal(stopped, ShiftHandoffRule.EvaluateRecipient(toUser, "tech1", active).Severity == RuleSeverity.HardStop);
}
