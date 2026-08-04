using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules.Engine;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// The authoring help is generated from the rule catalog rather than written by hand, so these
/// tests pin the property that makes that worthwhile: anything the engine accepts is described,
/// and nothing is described that the engine does not accept.
/// </summary>
public class RuleHelpTests
{
    [Fact]
    public void Help_DescribesEveryAttributeFunctionAndAction()
    {
        var help = RuleDefinitionAdminService.Help();

        Assert.Equal(
            RuleAttributeCatalog.AllAttributesForHelp().Select(a => a.Path).OrderBy(p => p),
            help.Attributes.Select(a => a.Path).OrderBy(p => p));

        Assert.Equal(
            RuleAttributeCatalog.AllFunctionsForHelp().Select(f => f.Name).OrderBy(n => n),
            help.Functions.Select(f => f.Name).OrderBy(n => n));

        Assert.Equal(
            RuleActionParser.Descriptors.Select(a => a.Name).OrderBy(n => n),
            help.Actions.Select(a => a.Name).OrderBy(n => n));
    }

    [Fact]
    public void Help_CoversBothLevelsThatTheEditorCanAuthor()
    {
        var help = RuleDefinitionAdminService.Help();

        foreach (var level in new[] { RuleLevel.Order, RuleLevel.Test })
        {
            var vocabulary = RuleDefinitionAdminService.Vocabulary(level);

            Assert.All(vocabulary.Attributes, a =>
                Assert.Contains(help.Attributes, h => h.Path == a.Path));
            Assert.All(vocabulary.Functions, f =>
                Assert.Contains(help.Functions, h => h.Name == f.Name));
            Assert.All(vocabulary.Actions, a =>
                Assert.Contains(help.Actions, h => h.Name == a.Name));
        }
    }

    [Fact]
    public void Help_EntriesCarryDescriptionAndExample()
    {
        var help = RuleDefinitionAdminService.Help();

        Assert.All(help.Attributes, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Description));
            Assert.False(string.IsNullOrWhiteSpace(a.Example));
            Assert.False(string.IsNullOrWhiteSpace(a.AvailableTo));
        });
        Assert.All(help.Functions, f =>
        {
            Assert.False(string.IsNullOrWhiteSpace(f.Description));
            Assert.False(string.IsNullOrWhiteSpace(f.Example));
        });
        Assert.All(help.Operators, o =>
        {
            Assert.False(string.IsNullOrWhiteSpace(o.Description));
            Assert.False(string.IsNullOrWhiteSpace(o.Example));
        });
        Assert.All(help.Actions, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Description));
            Assert.False(string.IsNullOrWhiteSpace(a.Example));
        });
    }

    [Fact]
    public void Help_MarksTestOnlyAttributesAndOrderOnlyActions()
    {
        var help = RuleDefinitionAdminService.Help();

        var interpretation = Assert.Single(help.Attributes, a => a.Path == "test.interpretation");
        Assert.Equal("Test rules only", interpretation.AvailableTo);

        var ageDays = Assert.Single(help.Attributes, a => a.Path == "patient.ageDays");
        Assert.Equal("Order and Test rules", ageDays.AvailableTo);

        var block = Assert.Single(help.Actions, a => a.Name == "block");
        Assert.Equal("Order rules only", block.AvailableTo);

        var addTest = Assert.Single(help.Actions, a => a.Name == "addTest");
        Assert.Equal("Order and Test rules", addTest.AvailableTo);
    }

    /// <summary>
    /// Each documented condition example is run through the real parser and validator, so a help
    /// entry cannot demonstrate syntax or an attribute the engine would reject.
    /// </summary>
    [Fact]
    public void Help_ConditionExamplesParseAgainstTheEngine()
    {
        var help = RuleDefinitionAdminService.Help();

        var conditions = help.Attributes.Select(a => a.Example)
            .Concat(help.Functions.Select(f => f.Example))
            .Concat(help.Operators.Select(o => o.Example));

        foreach (var example in conditions)
        {
            // Test level is the widest vocabulary, so every documented example must be legal there.
            var node = RuleExpressionParser.Parse(example);
            var unknown = RuleAttributeCatalog.FindUnknownReferences(node, RuleLevel.Test);
            Assert.True(unknown.Count == 0, $"'{example}' references {string.Join(", ", unknown)}");
        }
    }

    [Fact]
    public void Help_ActionExamplesParseAgainstTheEngine()
    {
        var help = RuleDefinitionAdminService.Help();

        foreach (var action in help.Actions)
        {
            var level = action.Name == "block" ? RuleLevel.Order : RuleLevel.Test;
            var parsed = RuleActionParser.Parse(action.Example, level);
            Assert.Single(parsed);
        }
    }
}
