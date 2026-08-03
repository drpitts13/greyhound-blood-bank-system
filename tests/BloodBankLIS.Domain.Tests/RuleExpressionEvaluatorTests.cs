using BloodBankLIS.Domain.Rules.Engine;

namespace BloodBankLIS.Domain.Tests;

public class RuleExpressionEvaluatorTests
{
    private static RuleFactBag Facts() => new RuleFactBag()
        .Set("patient.ageDays", 0)
        .Set("patient.ageYears", 0)
        .Set("patient.sex", "Female")
        .Set("patient.abo", "A")
        .Set("patient.rh", "Negative")
        .Set("patient.bloodType", "A Negative")
        .Set("order.priority", "Stat")
        .Set("order.specimenType", (string?)null)
        .Set("order.date", new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc))
        .SetList("order.tests", new[] { "TS", "ABORH" })
        .Set("test.code", "ABORH")
        .Set("test.interpretation", "A Negative")
        .SetFunction("order.hasTest", args =>
            RuleValue.FromBoolean(string.Equals(args[0].AsText(), "TS", StringComparison.OrdinalIgnoreCase)));

    private static bool Eval(string expression, IRuleFactSource? facts = null) =>
        RuleExpressionEvaluator.IsSatisfied(RuleExpressionParser.Parse(expression), facts ?? Facts());

    [Fact]
    public void NeonatalTypeAndScreenExampleMatches()
    {
        Assert.True(Eval("patient.ageDays < 1 AND order.hasTest('TS')"));
    }

    [Fact]
    public void NeonatalExampleDoesNotMatchOlderPatient()
    {
        var facts = Facts().Set("patient.ageDays", 2);

        Assert.False(Eval("patient.ageDays < 1 AND order.hasTest('TS')", facts));
    }

    [Fact]
    public void WeakDExampleMatchesOnInterpretation()
    {
        Assert.True(Eval(
            "test.code = 'ABORH' AND test.interpretation IN ('A Negative','B Negative','O Negative','AB Negative')"));
    }

    [Fact]
    public void WeakDExampleDoesNotMatchPositiveType()
    {
        var facts = Facts().Set("test.interpretation", "A Positive");

        Assert.False(Eval(
            "test.code = 'ABORH' AND test.interpretation IN ('A Negative','B Negative','O Negative','AB Negative')",
            facts));
    }

    [Fact]
    public void TextComparisonIsCaseInsensitiveAndTrimmed()
    {
        var facts = Facts().Set("patient.sex", "  female ");

        Assert.True(Eval("patient.sex = 'FEMALE'", facts));
    }

    [Fact]
    public void NumericComparisonCoercesQuotedNumbers()
    {
        var facts = Facts().Set("patient.ageDays", 10);

        Assert.True(Eval("patient.ageDays >= '10'", facts));
        Assert.False(Eval("patient.ageDays > '10'", facts));
    }

    [Fact]
    public void DateComparisonAgainstIsoLiteral()
    {
        Assert.True(Eval("order.date >= '2026-01-01'"));
        Assert.False(Eval("order.date >= '2027-01-01'"));
    }

    [Fact]
    public void ListContainsChecksMembership()
    {
        Assert.True(Eval("order.tests CONTAINS 'ABORH'"));
        Assert.False(Eval("order.tests CONTAINS 'DAT'"));
    }

    [Fact]
    public void TextContainsChecksSubstring()
    {
        var facts = Facts().Set("patient.bloodType", "A Negative");

        Assert.True(Eval("patient.bloodType CONTAINS 'negative'", facts));
    }

    [Fact]
    public void NullAttributeNeverMatchesComparison()
    {
        Assert.False(Eval("order.specimenType = 'EDTA'"));
        Assert.False(Eval("order.specimenType != 'EDTA'"));
        Assert.True(Eval("order.specimenType IS NULL"));
        Assert.False(Eval("order.specimenType IS NOT NULL"));
    }

    [Fact]
    public void NegatedOperatorsFailClosedOnNull()
    {
        Assert.False(Eval("order.specimenType NOT IN ('EDTA','SERUM')"));
    }

    [Fact]
    public void MissingAttributeEvaluatesToNullRatherThanThrowing()
    {
        Assert.False(Eval("patient.notSupplied = 'x'"));
        Assert.True(Eval("patient.notSupplied IS NULL"));
    }

    [Fact]
    public void MissingFunctionEvaluatesToNull()
    {
        Assert.False(Eval("order.hasProduct('E0336')"));
    }

    [Fact]
    public void NotNegatesResult()
    {
        Assert.False(Eval("NOT order.hasTest('TS')"));
        Assert.True(Eval("NOT order.hasTest('ABORH')"));
    }

    [Fact]
    public void OrShortCircuitsToTrue()
    {
        Assert.True(Eval("patient.ageDays > 900 OR patient.sex = 'Female'"));
    }

    [Fact]
    public void NotInExcludesMembers()
    {
        Assert.True(Eval("order.priority NOT IN ('Routine','PreOp')"));
        Assert.False(Eval("order.priority NOT IN ('Routine','Stat')"));
    }

    [Fact]
    public void BareFunctionCallIsTruthy()
    {
        Assert.True(Eval("order.hasTest('TS')"));
    }
}
