using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Pure eligibility checks for performing a product modification. Callers supply
/// plain snapshots of the candidate unit(s) and the request shape so the rule stays
/// deterministic and unit-testable (see docs/safety-rules.md).
/// </summary>
public static class UnitModificationEligibilityRule
{
    public const string StatusInvalidCode = "MOD-STATUS-INVALID";
    public const string ExpiredCode = "MOD-EXPIRED";
    public const string ProductMismatchCode = "MOD-PRODUCT-MISMATCH";
    public const string PoolMinSourcesCode = "MOD-POOL-MIN-SOURCES";
    public const string PoolAboMismatchCode = "MOD-POOL-ABO-MISMATCH";
    public const string DivideMinTargetsCode = "MOD-DIVIDE-MIN-TARGETS";
    public const string VolumeExceedsSourceCode = "MOD-VOLUME-EXCEEDS-SOURCE";

    public readonly record struct SourceUnitSnapshot(
        long Id,
        UnitStatus Status,
        long ProductTypeId,
        AboGroup Abo,
        RhType RhD,
        DateTime ExpiresUtc,
        decimal? Volume);

    /// <summary>Checks a single source unit against the rule's expected source product and the clock.</summary>
    public static RuleEvaluation EvaluateSource(SourceUnitSnapshot unit, long ruleSourceProductTypeId, DateTime nowUtc)
    {
        var results = new List<RuleResult>();

        if (unit.Status != UnitStatus.Available)
        {
            results.Add(RuleResult.HardStop(StatusInvalidCode,
                $"Unit status {unit.Status} is not eligible for modification (must be Available)."));
        }

        if (unit.ExpiresUtc <= nowUtc)
        {
            results.Add(RuleResult.HardStop(ExpiredCode, $"Unit expired at {unit.ExpiresUtc:u}."));
        }

        if (unit.ProductTypeId != ruleSourceProductTypeId)
        {
            results.Add(RuleResult.HardStop(ProductMismatchCode,
                "Unit's product type does not match the modification rule's source product."));
        }

        if (results.Count == 0)
        {
            results.Add(RuleResult.Pass(StatusInvalidCode));
        }

        return new RuleEvaluation(results);
    }

    /// <summary>Cross-unit checks for a Pool: at least two sources, identical ABO/Rh across all sources.</summary>
    public static RuleEvaluation EvaluatePool(IReadOnlyList<SourceUnitSnapshot> sources)
    {
        var results = new List<RuleResult>();

        if (sources.Count < 2)
        {
            results.Add(RuleResult.HardStop(PoolMinSourcesCode, "Pooling requires at least two source units."));
        }

        if (sources.Select(s => s.Abo).Distinct().Count() > 1 || sources.Select(s => s.RhD).Distinct().Count() > 1)
        {
            results.Add(RuleResult.HardStop(PoolAboMismatchCode, "All pooled source units must share the same ABO/Rh."));
        }

        if (results.Count == 0)
        {
            results.Add(RuleResult.Pass(PoolMinSourcesCode));
        }

        return new RuleEvaluation(results);
    }

    /// <summary>Checks for a Divide: at least two result children, and (if volumes given) they must not exceed the source's volume.</summary>
    public static RuleEvaluation EvaluateDivide(int childCount, decimal? sourceVolume, IReadOnlyList<decimal?> childVolumes)
    {
        var results = new List<RuleResult>();

        if (childCount < 2)
        {
            results.Add(RuleResult.HardStop(DivideMinTargetsCode, "Dividing a unit requires at least two result units."));
        }

        if (sourceVolume is not null && childVolumes.Count > 0 && childVolumes.All(v => v is not null))
        {
            var sum = childVolumes.Sum(v => v ?? 0m);
            if (sum > sourceVolume)
            {
                results.Add(RuleResult.HardStop(VolumeExceedsSourceCode,
                    $"Requested child volumes ({sum}) exceed the source unit's volume ({sourceVolume})."));
            }
        }

        if (results.Count == 0)
        {
            results.Add(RuleResult.Pass(DivideMinTargetsCode));
        }

        return new RuleEvaluation(results);
    }
}
