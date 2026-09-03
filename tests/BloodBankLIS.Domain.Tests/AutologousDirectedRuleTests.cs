using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class AutologousDirectedRuleTests
{
    [Fact]
    public void AllogeneicReceive_PassesWithoutRecipient()
    {
        var result = AutologousDirectedRule.EvaluateReceive(DonationRestriction.Allogeneic, null);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void AutologousReceive_MissingRecipient_IsHardStop()
    {
        var result = AutologousDirectedRule.EvaluateReceive(DonationRestriction.Autologous, null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AutologousDirectedRule.ReceiveCode, result.Code);
    }

    [Fact]
    public void DirectedReceive_WithRecipient_Passes()
    {
        var result = AutologousDirectedRule.EvaluateReceive(DonationRestriction.Directed, 12);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void AutologousIssue_MatchingPatient_Passes()
    {
        var result = AutologousDirectedRule.EvaluateIssue(DonationRestriction.Autologous, 7, 7);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void AutologousIssue_WrongPatient_IsHardStop()
    {
        var result = AutologousDirectedRule.EvaluateIssue(DonationRestriction.Autologous, 7, 99);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AutologousDirectedRule.IssueCode, result.Code);
    }

    [Fact]
    public void DirectedIssue_WrongPatient_IsHardStop()
    {
        var result = AutologousDirectedRule.EvaluateIssue(DonationRestriction.Directed, 3, 4);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void AllogeneicIssue_IgnoresReservedPatient()
    {
        var result = AutologousDirectedRule.EvaluateIssue(DonationRestriction.Allogeneic, 7, 99);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Convert_DirectedAvailable_Passes()
    {
        var result = AutologousDirectedRule.EvaluateConvert(DonationRestriction.Directed, UnitStatus.Available);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
        Assert.Equal(AutologousDirectedRule.ConvertCode, result.Code);
    }

    [Fact]
    public void Convert_Autologous_IsHardStop()
    {
        var result = AutologousDirectedRule.EvaluateConvert(DonationRestriction.Autologous, UnitStatus.Quarantine);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AutologousDirectedRule.ConvertCode, result.Code);
    }

    [Fact]
    public void Convert_Allogeneic_IsHardStop()
    {
        var result = AutologousDirectedRule.EvaluateConvert(DonationRestriction.Allogeneic, UnitStatus.Available);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void Convert_AllocatedDirected_IsHardStop()
    {
        var result = AutologousDirectedRule.EvaluateConvert(DonationRestriction.Directed, UnitStatus.Allocated);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void Convert_IssuedDirected_IsHardStop()
    {
        var result = AutologousDirectedRule.EvaluateConvert(DonationRestriction.Directed, UnitStatus.Issued);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }
}
