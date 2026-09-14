using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class AuthSessionValidityRuleTests
{
    private static readonly DateTime Now = new(2026, 9, 5, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ValidSession_Passes()
    {
        var result = AuthSessionValidityRule.Evaluate(
            Now, null, Now.AddHours(12), Now.AddMinutes(30), userIsActive: true, userIsLocked: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Revoked_IsHardStop()
    {
        var result = AuthSessionValidityRule.Evaluate(
            Now, Now, Now.AddHours(12), Now.AddMinutes(30), true, false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AuthSessionValidityRule.RevokedCode, result.Code);
    }

    [Fact]
    public void AbsoluteExpiry_IsHardStop()
    {
        var result = AuthSessionValidityRule.Evaluate(
            Now, null, Now.AddMinutes(-1), Now.AddMinutes(30), true, false);
        Assert.Equal(AuthSessionValidityRule.AbsoluteExpiredCode, result.Code);
    }

    [Fact]
    public void IdleExpiry_IsHardStop()
    {
        var result = AuthSessionValidityRule.Evaluate(
            Now, null, Now.AddHours(12), Now.AddMinutes(-1), true, false);
        Assert.Equal(AuthSessionValidityRule.IdleExpiredCode, result.Code);
    }

    [Fact]
    public void InactiveUser_IsHardStop()
    {
        var result = AuthSessionValidityRule.Evaluate(
            Now, null, Now.AddHours(12), Now.AddMinutes(30), userIsActive: false, userIsLocked: false);
        Assert.Equal(AuthSessionValidityRule.InactiveCode, result.Code);
    }

    [Fact]
    public void LockedUser_IsHardStop()
    {
        var result = AuthSessionValidityRule.Evaluate(
            Now, null, Now.AddHours(12), Now.AddMinutes(30), true, userIsLocked: true);
        Assert.Equal(AuthSessionValidityRule.LockedCode, result.Code);
    }
}
