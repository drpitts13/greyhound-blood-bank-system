using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules.Engine;

namespace BloodBankLIS.Domain.Tests;

public class RuleExpressionParserTests
{
    [Fact]
    public void ParsesComparisonAgainstNumberLiteral()
    {
        var node = RuleExpressionParser.Parse("patient.ageDays < 1");

        var binary = Assert.IsType<RuleBinaryNode>(node);
        Assert.Equal(RuleBinaryOperator.LessThan, binary.Operator);
        Assert.Equal("patient.ageDays", Assert.IsType<RuleAttributeNode>(binary.Left).Path);
        Assert.Equal(RuleValueKind.Number, Assert.IsType<RuleLiteralNode>(binary.Right).Value.Kind);
    }

    [Fact]
    public void AndBindsTighterThanOr()
    {
        // a OR b AND c parses as a OR (b AND c)
        var node = RuleExpressionParser.Parse("patient.sex = 'Male' OR patient.ageYears > 1 AND patient.abo = 'O'");

        var root = Assert.IsType<RuleBinaryNode>(node);
        Assert.Equal(RuleBinaryOperator.Or, root.Operator);
        Assert.Equal(RuleBinaryOperator.And, Assert.IsType<RuleBinaryNode>(root.Right).Operator);
    }

    [Fact]
    public void ParenthesesOverridePrecedence()
    {
        var node = RuleExpressionParser.Parse("(patient.sex = 'Male' OR patient.ageYears > 1) AND patient.abo = 'O'");

        var root = Assert.IsType<RuleBinaryNode>(node);
        Assert.Equal(RuleBinaryOperator.And, root.Operator);
        Assert.Equal(RuleBinaryOperator.Or, Assert.IsType<RuleBinaryNode>(root.Left).Operator);
    }

    [Fact]
    public void ParsesInListWithMultipleItems()
    {
        var node = RuleExpressionParser.Parse(
            "test.interpretation IN ('A Negative','B Negative','O Negative','AB Negative')");

        var binary = Assert.IsType<RuleBinaryNode>(node);
        Assert.Equal(RuleBinaryOperator.In, binary.Operator);
        Assert.Equal(4, Assert.IsType<RuleListNode>(binary.Right).Items.Count);
    }

    [Fact]
    public void ParsesNotIn()
    {
        var node = RuleExpressionParser.Parse("order.priority NOT IN ('Stat','Urgent')");

        Assert.Equal(RuleBinaryOperator.NotIn, Assert.IsType<RuleBinaryNode>(node).Operator);
    }

    [Fact]
    public void ParsesFunctionCall()
    {
        var node = RuleExpressionParser.Parse("order.hasTest('TS')");

        var function = Assert.IsType<RuleFunctionNode>(node);
        Assert.Equal("order.hasTest", function.Name);
        Assert.Single(function.Arguments);
    }

    [Fact]
    public void ParsesNotPrefix()
    {
        var node = RuleExpressionParser.Parse("NOT order.hasTest('TS')");

        Assert.Equal(RuleUnaryOperator.Not, Assert.IsType<RuleUnaryNode>(node).Operator);
    }

    [Fact]
    public void ParsesIsNullAndIsNotNull()
    {
        Assert.Equal(RuleUnaryOperator.IsNull,
            Assert.IsType<RuleUnaryNode>(RuleExpressionParser.Parse("order.specimenType IS NULL")).Operator);
        Assert.Equal(RuleUnaryOperator.IsNotNull,
            Assert.IsType<RuleUnaryNode>(RuleExpressionParser.Parse("order.specimenType IS NOT NULL")).Operator);
    }

    [Theory]
    [InlineData("!=")]
    [InlineData("<>")]
    public void BothNotEqualSpellingsParse(string op)
    {
        var node = RuleExpressionParser.Parse($"patient.sex {op} 'Male'");

        Assert.Equal(RuleBinaryOperator.NotEqual, Assert.IsType<RuleBinaryNode>(node).Operator);
    }

    [Fact]
    public void EscapedQuoteInsideTextLiteral()
    {
        var node = RuleExpressionParser.Parse("patient.sex = 'it''s'");

        var literal = Assert.IsType<RuleLiteralNode>(Assert.IsType<RuleBinaryNode>(node).Right);
        Assert.Equal("it's", literal.Value.AsText());
    }

    [Theory]
    [InlineData("")]
    [InlineData("patient.ageDays <")]
    [InlineData("patient.ageDays < 1 AND")]
    [InlineData("(patient.ageDays < 1")]
    [InlineData("patient.ageDays IN 1")]
    [InlineData("order.hasTest('TS'")]
    [InlineData("patient.sex = 'unterminated")]
    [InlineData("patient.ageDays < 1 $ 2")]
    public void InvalidSyntaxIsReported(string expression)
    {
        Assert.False(RuleExpressionParser.TryParse(expression, out var node, out var error));
        Assert.Null(node);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void UnknownAttributeIsRejected()
    {
        var node = RuleExpressionParser.Parse("patient.favouriteColour = 'blue'");

        var errors = RuleAttributeCatalog.FindUnknownReferences(node, RuleLevel.Order);

        Assert.Contains(errors, e => e.Contains("Unknown attribute", StringComparison.Ordinal));
    }

    [Fact]
    public void TestAttributeIsRejectedAtOrderLevel()
    {
        var node = RuleExpressionParser.Parse("test.code = 'ABORH'");

        Assert.Contains(
            RuleAttributeCatalog.FindUnknownReferences(node, RuleLevel.Order),
            e => e.Contains("only available to test-level", StringComparison.Ordinal));
        Assert.Empty(RuleAttributeCatalog.FindUnknownReferences(node, RuleLevel.Test));
    }

    [Fact]
    public void OrderAttributesAreAvailableToTestLevelRules()
    {
        var node = RuleExpressionParser.Parse("patient.ageDays < 1 AND order.hasTest('TS')");

        Assert.Empty(RuleAttributeCatalog.FindUnknownReferences(node, RuleLevel.Test));
    }

    [Fact]
    public void UnknownFunctionIsRejected()
    {
        var node = RuleExpressionParser.Parse("order.hasWidget('X')");

        Assert.Contains(
            RuleAttributeCatalog.FindUnknownReferences(node, RuleLevel.Order),
            e => e.Contains("Unknown function", StringComparison.Ordinal));
    }

    [Fact]
    public void WrongArgumentCountIsRejected()
    {
        var node = RuleExpressionParser.Parse("order.hasTest('A','B')");

        Assert.Contains(
            RuleAttributeCatalog.FindUnknownReferences(node, RuleLevel.Order),
            e => e.Contains("takes 1 argument", StringComparison.Ordinal));
    }

    [Fact]
    public void BareFunctionAliasResolves()
    {
        Assert.NotNull(RuleAttributeCatalog.ResolveFunction("hasTest", RuleLevel.Order));
        Assert.NotNull(RuleAttributeCatalog.ResolveFunction("HASTEST", RuleLevel.Order));
        Assert.Null(RuleAttributeCatalog.ResolveFunction("subtest", RuleLevel.Order));
        Assert.NotNull(RuleAttributeCatalog.ResolveFunction("subtest", RuleLevel.Test));
    }
}
