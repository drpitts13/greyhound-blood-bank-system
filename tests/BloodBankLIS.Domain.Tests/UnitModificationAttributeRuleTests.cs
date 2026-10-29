using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class UnitModificationAttributeRuleTests
{
    [Fact]
    public void SingleSource_CopiesTestedResults_AndSkipsNotTested()
    {
        var inheritance = UnitModificationAttributeRule.Evaluate(
        [
            [
                Fact(10, AntigenResult.Negative),
                Fact(11, AntigenResult.NotTested)
            ]
        ]);

        Assert.False(inheritance.Evaluation.IsHardStopped);
        var copied = Assert.Single(inheritance.Inherited);
        Assert.Equal(10, copied.DefinitionId);
        Assert.Equal(AntigenResult.Negative, copied.Result);
    }

    [Fact]
    public void Pool_DiscordantResults_HardStops()
    {
        var inheritance = UnitModificationAttributeRule.Evaluate(
        [
            [Fact(10, AntigenResult.Positive)],
            [Fact(10, AntigenResult.Negative)]
        ]);

        Assert.True(inheritance.Evaluation.IsHardStopped);
        Assert.Contains(
            inheritance.Evaluation.HardStops,
            stop => stop.Code == UnitModificationAttributeRule.DiscordantCode);
    }

    [Fact]
    public void Pool_PositiveAndUntyped_InheritsPositive()
    {
        var inheritance = UnitModificationAttributeRule.Evaluate(
        [
            [Fact(10, AntigenResult.Positive)],
            []
        ]);

        Assert.False(inheritance.Evaluation.IsHardStopped);
        Assert.Equal(AntigenResult.Positive, Assert.Single(inheritance.Inherited).Result);
    }

    [Fact]
    public void Pool_AllNegative_InheritsNegative()
    {
        var inheritance = UnitModificationAttributeRule.Evaluate(
        [
            [Fact(10, AntigenResult.Negative)],
            [Fact(10, AntigenResult.Negative)]
        ]);

        Assert.False(inheritance.Evaluation.IsHardStopped);
        Assert.Equal(AntigenResult.Negative, Assert.Single(inheritance.Inherited).Result);
    }

    [Fact]
    public void Pool_PartialNegative_DoesNotClaimNegative()
    {
        var inheritance = UnitModificationAttributeRule.Evaluate(
        [
            [Fact(10, AntigenResult.Negative)],
            []
        ]);

        Assert.False(inheritance.Evaluation.IsHardStopped);
        Assert.Empty(inheritance.Inherited);
    }

    private static UnitModificationAttributeRule.AttributeFact Fact(long id, AntigenResult result) =>
        new(id, BloodAttributeKind.Antigen, result, UnitAttributeTypingSource.InHouse, null);
}
