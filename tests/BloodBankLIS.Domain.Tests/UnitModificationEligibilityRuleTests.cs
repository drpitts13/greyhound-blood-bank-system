using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class UnitModificationEligibilityRuleTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static UnitModificationEligibilityRule.SourceUnitSnapshot MakeUnit(
        long id = 1,
        UnitStatus status = UnitStatus.Available,
        long productTypeId = 100,
        AboGroup abo = AboGroup.O,
        RhType rh = RhType.Positive,
        DateTime? expiresUtc = null,
        decimal? volume = null) =>
        new(id, status, productTypeId, abo, rh, expiresUtc ?? NowUtc.AddDays(10), volume);

    [Fact]
    public void EvaluateSource_ValidUnit_Passes()
    {
        var unit = MakeUnit();

        var eval = UnitModificationEligibilityRule.EvaluateSource(unit, ruleSourceProductTypeId: 100, NowUtc);

        Assert.False(eval.IsHardStopped);
    }

    [Fact]
    public void EvaluateSource_NotAvailable_HardStops()
    {
        var unit = MakeUnit(status: UnitStatus.Quarantine);

        var eval = UnitModificationEligibilityRule.EvaluateSource(unit, ruleSourceProductTypeId: 100, NowUtc);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == UnitModificationEligibilityRule.StatusInvalidCode);
    }

    [Fact]
    public void EvaluateSource_Expired_HardStops()
    {
        var unit = MakeUnit(expiresUtc: NowUtc.AddDays(-1));

        var eval = UnitModificationEligibilityRule.EvaluateSource(unit, ruleSourceProductTypeId: 100, NowUtc);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == UnitModificationEligibilityRule.ExpiredCode);
    }

    [Fact]
    public void EvaluateSource_ExpiresExactlyNow_HardStops()
    {
        var unit = MakeUnit(expiresUtc: NowUtc);

        var eval = UnitModificationEligibilityRule.EvaluateSource(unit, ruleSourceProductTypeId: 100, NowUtc);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == UnitModificationEligibilityRule.ExpiredCode);
    }

    [Fact]
    public void EvaluateSource_ProductMismatch_HardStops()
    {
        var unit = MakeUnit(productTypeId: 200);

        var eval = UnitModificationEligibilityRule.EvaluateSource(unit, ruleSourceProductTypeId: 100, NowUtc);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == UnitModificationEligibilityRule.ProductMismatchCode);
    }

    [Fact]
    public void EvaluatePool_TwoMatchingSources_Passes()
    {
        var sources = new[] { MakeUnit(id: 1), MakeUnit(id: 2) };

        var eval = UnitModificationEligibilityRule.EvaluatePool(sources);

        Assert.False(eval.IsHardStopped);
    }

    [Fact]
    public void EvaluatePool_SingleSource_HardStops()
    {
        var sources = new[] { MakeUnit(id: 1) };

        var eval = UnitModificationEligibilityRule.EvaluatePool(sources);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == UnitModificationEligibilityRule.PoolMinSourcesCode);
    }

    [Fact]
    public void EvaluatePool_AboMismatch_HardStops()
    {
        var sources = new[] { MakeUnit(id: 1, abo: AboGroup.O), MakeUnit(id: 2, abo: AboGroup.A) };

        var eval = UnitModificationEligibilityRule.EvaluatePool(sources);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == UnitModificationEligibilityRule.PoolAboMismatchCode);
    }

    [Fact]
    public void EvaluatePool_RhMismatch_HardStops()
    {
        var sources = new[] { MakeUnit(id: 1, rh: RhType.Positive), MakeUnit(id: 2, rh: RhType.Negative) };

        var eval = UnitModificationEligibilityRule.EvaluatePool(sources);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == UnitModificationEligibilityRule.PoolAboMismatchCode);
    }

    [Fact]
    public void EvaluateDivide_TwoChildrenNoVolumes_Passes()
    {
        var eval = UnitModificationEligibilityRule.EvaluateDivide(childCount: 2, sourceVolume: null, childVolumes: []);

        Assert.False(eval.IsHardStopped);
    }

    [Fact]
    public void EvaluateDivide_SingleChild_HardStops()
    {
        var eval = UnitModificationEligibilityRule.EvaluateDivide(childCount: 1, sourceVolume: null, childVolumes: []);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == UnitModificationEligibilityRule.DivideMinTargetsCode);
    }

    [Fact]
    public void EvaluateDivide_VolumesWithinSource_Passes()
    {
        var eval = UnitModificationEligibilityRule.EvaluateDivide(childCount: 2, sourceVolume: 300m, childVolumes: [150m, 150m]);

        Assert.False(eval.IsHardStopped);
    }

    [Fact]
    public void EvaluateDivide_VolumesExceedSource_HardStops()
    {
        var eval = UnitModificationEligibilityRule.EvaluateDivide(childCount: 2, sourceVolume: 200m, childVolumes: [150m, 150m]);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == UnitModificationEligibilityRule.VolumeExceedsSourceCode);
    }

    [Fact]
    public void EvaluateDivide_PartialVolumes_SkipsVolumeCheck()
    {
        var eval = UnitModificationEligibilityRule.EvaluateDivide(childCount: 2, sourceVolume: 100m, childVolumes: [150m, null]);

        Assert.False(eval.IsHardStopped);
    }
}
