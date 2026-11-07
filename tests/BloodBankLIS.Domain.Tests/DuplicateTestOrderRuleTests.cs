using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-502: duplicate testing warns per test, naming the open orders; nothing open, nothing said.
public class DuplicateTestOrderRuleTests
{
    [Fact]
    public void NamesEachRepeatedTest_AndItsOpenOrders()
    {
        var open = new[]
        {
            new DuplicateTestOrderRule.OpenLine("ABSC", "ORD2"), new DuplicateTestOrderRule.OpenLine("absc", "ORD1"),
            new DuplicateTestOrderRule.OpenLine("DAT", "ORD3"), new DuplicateTestOrderRule.OpenLine("XM", "ORD4")
        };

        var result = DuplicateTestOrderRule.Evaluate(["ABSC", " dat ", "ABORH"], open);

        Assert.NotNull(result);
        Assert.Equal(RuleSeverity.Warning, result!.Severity);
        Assert.Contains("ABSC on ORD1, ORD2; DAT on ORD3.", result.Message);
        Assert.DoesNotContain("XM", result.Message);
        Assert.Null(DuplicateTestOrderRule.Evaluate(["ABORH"], open));
        Assert.Null(DuplicateTestOrderRule.Evaluate([], open));
    }
}
