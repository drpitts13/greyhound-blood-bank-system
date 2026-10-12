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
    public const string VolumeRequiredCode = "MOD-DIVIDE-VOLUME-REQUIRED";
    public const string VolumeExceedsSourceCode = "MOD-VOLUME-EXCEEDS-SOURCE";
    public const string AliquotVolumeCode = "MOD-ALIQUOT-VOLUME";
    public const string AliquotParentVolumeCode = "MOD-ALIQUOT-PARENT-VOLUME";

    public readonly record struct SourceUnitSnapshot(
        long Id,
        UnitStatus Status,
        long ProductTypeId,
        AboGroup Abo,
        RhType RhD,
        DateTime ExpiresUtc,
        decimal? Volume);

    /// <summary>
    /// Checks a single source unit against the rule's expected source product and the clock.
    /// Standalone modify requires <see cref="UnitStatus.Available"/>. Order-linked 1:1
    /// modify also accepts <see cref="UnitStatus.Assigned"/>.
    /// </summary>
    public static RuleEvaluation EvaluateSource(
        SourceUnitSnapshot unit,
        long ruleSourceProductTypeId,
        DateTime nowUtc,
        bool allowAssigned = false)
    {
        var results = new List<RuleResult>();

        var statusOk = unit.Status == UnitStatus.Available
            || (allowAssigned && unit.Status == UnitStatus.Assigned);
        if (!statusOk)
        {
            var allowed = allowAssigned ? "Available or Assigned" : "Available";
            results.Add(RuleResult.HardStop(StatusInvalidCode,
                $"Unit status {unit.Status} is not eligible for modification (must be {allowed})."));
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

    /// <summary>Checks for a Divide: at least two result children, every child has a volume, and the volumes do not exceed the source.</summary>
    public static RuleEvaluation EvaluateDivide(int childCount, decimal? sourceVolume, IReadOnlyList<decimal?> childVolumes)
    {
        var results = new List<RuleResult>();

        if (childCount < 2)
        {
            results.Add(RuleResult.HardStop(DivideMinTargetsCode, "Dividing a unit requires at least two result units."));
        }

        var volumes = childVolumes ?? Array.Empty<decimal?>();
        if (volumes.Count != childCount || volumes.Any(v => v is null or <= 0m))
        {
            results.Add(RuleResult.HardStop(VolumeRequiredCode,
                "Each result unit of a divide must have a volume greater than zero."));
        }
        else if (sourceVolume is not null)
        {
            var sum = volumes.Sum(v => v ?? 0m);
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

    /// <summary>
    /// An aliquot removes a positive volume that is strictly less than the parent volume,
    /// so the parent remains in inventory.
    /// </summary>
    public static RuleEvaluation EvaluateAliquot(decimal? sourceVolume, decimal? aliquotVolume)
    {
        if (aliquotVolume is null or <= 0m)
        {
            return new RuleEvaluation(new[]
            {
                RuleResult.HardStop(AliquotVolumeCode, "An aliquot volume must be greater than zero.")
            });
        }

        if (sourceVolume is null or <= 0m)
        {
            return new RuleEvaluation(new[]
            {
                RuleResult.HardStop(
                    AliquotParentVolumeCode,
                    "The parent unit needs a recorded volume before an aliquot can be removed.")
            });
        }

        if (aliquotVolume >= sourceVolume)
        {
            return new RuleEvaluation(new[]
            {
                RuleResult.HardStop(
                    AliquotParentVolumeCode,
                    "Aliquot volume must be less than the parent volume so the parent remains.")
            });
        }

        return new RuleEvaluation(new[] { RuleResult.Pass(AliquotVolumeCode) });
    }
}
