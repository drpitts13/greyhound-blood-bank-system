using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ReagentRegisterRuleTests
{
    // TEST-BB-983: a reagent needs a code and name not already in the register; a rename needs a name and a reason; a
    // reagent with lots received or open is not retired, and retiring needs a reason.
    [Fact]
    public void Register_Rules()
    {
        Assert.Equal(RuleSeverity.HardStop, ReagentRegisterRule.EvaluateCreate("", "Anti-A", false).Severity);
        Assert.Contains("already in the register", ReagentRegisterRule.EvaluateCreate("ANTI-A", "Anti-A", true).Message);
        Assert.Equal(RuleSeverity.Pass, ReagentRegisterRule.EvaluateCreate("ANTI-A", "Anti-A", false).Severity);

        Assert.Equal(RuleSeverity.HardStop, ReagentRegisterRule.EvaluateRename("Anti-A", null).Severity);
        Assert.Equal(RuleSeverity.HardStop, ReagentRegisterRule.EvaluateRename(" ", "typo").Severity);
        Assert.Equal(RuleSeverity.Pass, ReagentRegisterRule.EvaluateRename("Anti-A (monoclonal)", "typo").Severity);

        Assert.Contains("2 lots are still received or open", ReagentRegisterRule.EvaluateRetire("Discontinued", 2).Message);
        Assert.Equal(RuleSeverity.HardStop, ReagentRegisterRule.EvaluateRetire("", 0).Severity);
        Assert.Equal(RuleSeverity.Pass, ReagentRegisterRule.EvaluateRetire("Discontinued", 0).Severity);
    }
}
