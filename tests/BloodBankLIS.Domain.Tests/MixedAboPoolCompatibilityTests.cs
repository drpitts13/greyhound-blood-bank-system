using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class MixedAboPoolCompatibilityTests
{
    private static readonly AboRh MixedPos = new(AboGroup.Mixed, RhType.Positive);
    private static readonly PoolComponent APos = new(AboGroup.A, RhType.Positive);
    private static readonly PoolComponent OPos = new(AboGroup.O, RhType.Positive);

    // TEST-BB-1416 (mixed-ABO pooling phase 2): a bare Mixed group is refused on either check (it would carry no
    // antigens and pass anyone); a pool is compatible only if every component is. An A + O cryo pool, read in the
    // plasma direction (donor antibodies against recipient antigens), is refused for an A recipient by the O unit's
    // anti-A, refused for an AB recipient by both units, and accepted for an O recipient.
    [Fact]
    public void MixedPool_IsCheckedAgainstEachComponent_AndFailsClosedWithoutThem()
    {
        var aRecipient = new AboRh(AboGroup.A, RhType.Positive);
        Assert.Contains(AboCompatibilityRule.Evaluate(aRecipient, MixedPos, ComponentClass.Cryoprecipitate),
            r => r.Code == AboCompatibilityRule.MixedPoolCode && r.Severity == RuleSeverity.HardStop);
        Assert.Contains(AboCompatibilityRule.EvaluateUnit(aRecipient, MixedPos, null, ComponentClass.Cryoprecipitate),
            r => r.Code == AboCompatibilityRule.MixedPoolCode && r.Severity == RuleSeverity.HardStop);

        var refused = AboCompatibilityRule.EvaluateUnit(aRecipient, MixedPos, [APos, OPos], ComponentClass.Cryoprecipitate);
        var stop = Assert.Single(refused, r => r.Severity == RuleSeverity.HardStop);
        Assert.Equal(AboCompatibilityRule.AboCode, stop.Code);
        Assert.StartsWith("In this pool, the O Rh positive unit:", stop.Message);

        Assert.Equal(2, AboCompatibilityRule.EvaluateUnit(new AboRh(AboGroup.AB, RhType.Positive), MixedPos, [APos, OPos], ComponentClass.Cryoprecipitate)
            .Count(r => r.Severity == RuleSeverity.HardStop));
        Assert.DoesNotContain(AboCompatibilityRule.EvaluateUnit(new AboRh(AboGroup.O, RhType.Negative), MixedPos, [APos, OPos], ComponentClass.Cryoprecipitate),
            r => r.Severity == RuleSeverity.HardStop);
    }

    // TEST-BB-1416: a pool's group is the shared group or Mixed, and its Rh positive when any source is, unknown when any
    // is unknown; pooling across ABO is refused unless the target allows mixed pools.
    [Fact]
    public void PoolGroup_AndEligibility_FollowTheSetting()
    {
        Assert.Equal(AboGroup.O, PoolComponents.ResultAbo([OPos, OPos]));
        Assert.Equal(AboGroup.Mixed, PoolComponents.ResultAbo([APos, OPos]));
        Assert.Equal(RhType.Positive, PoolComponents.ResultRh([new(AboGroup.A, RhType.Negative), OPos]));
        Assert.Equal(RhType.Negative, PoolComponents.ResultRh([new(AboGroup.A, RhType.Negative), new(AboGroup.O, RhType.Negative)]));
        Assert.Equal(RhType.Unknown, PoolComponents.ResultRh([new(AboGroup.A, RhType.Unknown), OPos]));

        UnitModificationEligibilityRule.SourceUnitSnapshot Unit(AboGroup abo, RhType rh) =>
            new(1, UnitStatus.Available, 1, abo, rh, DateTime.UtcNow.AddDays(5), 10m);
        var mixed = new[] { Unit(AboGroup.A, RhType.Positive), Unit(AboGroup.O, RhType.Negative) };
        Assert.Contains(UnitModificationEligibilityRule.EvaluatePool(mixed).Results, r => r.Code == UnitModificationEligibilityRule.PoolAboMismatchCode);
        Assert.DoesNotContain(UnitModificationEligibilityRule.EvaluatePool(mixed, allowMixedAbo: true).Results, r => r.Severity == RuleSeverity.HardStop);
    }

    // TEST-BB-1416: Mixed is never offered where a person or single unit is typed.
    [Fact]
    public void Mixed_IsNotATypingChoice() => Assert.DoesNotContain(AboGroup.Mixed, AboGroups.All);
}
