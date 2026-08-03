using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules.Engine;

namespace BloodBankLIS.Domain.Tests;

public class RuleActionParserTests
{
    [Fact]
    public void ParsesChainedActions()
    {
        var actions = RuleActionParser.Parse("cancelTest('TS'); addTest('TSNEO')", RuleLevel.Order);

        Assert.Equal(2, actions.Count);
        Assert.Equal(RuleActionKind.CancelTest, actions[0].Kind);
        Assert.Equal("TS", actions[0].TestCode);
        Assert.Equal(RuleActionKind.AddTest, actions[1].Kind);
        Assert.Equal("TSNEO", actions[1].TestCode);
    }

    [Fact]
    public void TestCodeIsNormalizedToUpperCase()
    {
        var actions = RuleActionParser.Parse("addTest(' weakd ')", RuleLevel.Test);

        Assert.Equal("WEAKD", actions[0].TestCode);
    }

    [Fact]
    public void ActionNameIsCaseInsensitive()
    {
        var actions = RuleActionParser.Parse("ADDTEST('WEAKD')", RuleLevel.Test);

        Assert.Equal(RuleActionKind.AddTest, actions[0].Kind);
    }

    [Fact]
    public void TrailingSemicolonIsTolerated()
    {
        Assert.Single(RuleActionParser.Parse("addTest('WEAKD');", RuleLevel.Test));
    }

    [Fact]
    public void WarnPreservesMessageCasing()
    {
        var actions = RuleActionParser.Parse("warn('Neonatal protocol applies')", RuleLevel.Order);

        Assert.Equal(RuleActionKind.Warn, actions[0].Kind);
        Assert.Equal("Neonatal protocol applies", actions[0].Argument);
    }

    [Fact]
    public void BlockIsOrderLevelOnly()
    {
        Assert.Single(RuleActionParser.Parse("block('Not allowed')", RuleLevel.Order));

        Assert.False(RuleActionParser.TryParse("block('Not allowed')", RuleLevel.Test, out _, out var error));
        Assert.Contains("only available to Order-level", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("addTest")]
    [InlineData("addTest(")]
    [InlineData("addTest()")]
    [InlineData("addTest('')")]
    [InlineData("addTest(WEAKD)")]
    [InlineData("addTest('A') addTest('B')")]
    [InlineData("deleteEverything('A')")]
    public void InvalidActionsAreReported(string expression)
    {
        Assert.False(RuleActionParser.TryParse(expression, RuleLevel.Order, out var actions, out var error));
        Assert.Empty(actions);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void DescriptorsExcludeBlockForTestLevel()
    {
        Assert.DoesNotContain(RuleActionParser.For(RuleLevel.Test), d => d.Kind == RuleActionKind.Block);
        Assert.Contains(RuleActionParser.For(RuleLevel.Order), d => d.Kind == RuleActionKind.Block);
    }
}
