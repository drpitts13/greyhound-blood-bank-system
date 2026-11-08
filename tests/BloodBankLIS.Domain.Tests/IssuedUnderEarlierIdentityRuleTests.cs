using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-617: a unit applies when it was issued under another MRN (case and spaces ignored); the warning names the
// earlier identities once each and the current MRN.
public class IssuedUnderEarlierIdentityRuleTests
{
    [Fact]
    public void Applies_AndWarning()
    {
        Assert.True(IssuedUnderEarlierIdentityRule.Applies("UNK-20261108-01", "MRN0099"));
        Assert.False(IssuedUnderEarlierIdentityRule.Applies(" mrn0099 ", "MRN0099"));
        Assert.False(IssuedUnderEarlierIdentityRule.Applies(null, "MRN0099"));
        Assert.False(IssuedUnderEarlierIdentityRule.Applies(" ", "MRN0099"));

        var warning = IssuedUnderEarlierIdentityRule.Warning(2, ["UNK-1", "unk-1"], "MRN0099");
        Assert.Equal(RuleSeverity.Warning, warning.Severity);
        Assert.StartsWith("2 units are still out under UNK-1.", warning.Message);
        Assert.Contains("uses MRN MRN0099", warning.Message);
    }
}
