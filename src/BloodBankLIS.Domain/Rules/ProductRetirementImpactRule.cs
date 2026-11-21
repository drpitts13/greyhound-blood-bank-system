using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// What still uses a product being retired (end-user testing loop 2 tick 23): active modification rules that start from
/// it or make it, units of it still in inventory, and minimum stock levels set for it. Retiring is not refused; each use
/// is named so it can be updated.
/// </summary>
public static class ProductRetirementImpactRule
{
    public const string Code = "PRODUCT-RETIRE-IN-USE";

    /// <summary>Statuses of a unit still held in or expected into inventory.</summary>
    public static readonly IReadOnlySet<UnitStatus> InInventory = new HashSet<UnitStatus>
    {
        UnitStatus.Expected, UnitStatus.Received, UnitStatus.Quarantine, UnitStatus.Available, UnitStatus.Allocated,
        UnitStatus.Selected, UnitStatus.Assigned, UnitStatus.Crossmatched
    };

    public static IReadOnlyList<RuleResult> Warnings(
        string productCode,
        IReadOnlyList<string> rulesFromIt,
        IReadOnlyList<string> rulesMakingIt,
        int unitsInInventory,
        int minimumStockLevels)
    {
        var results = new List<RuleResult>();
        if (rulesFromIt.Count > 0)
        {
            results.Add(RuleResult.Warning(Code,
                $"{productCode} is still the source of {Rules(rulesFromIt)}. Update or deactivate {Them(rulesFromIt)} on Modification Rules."));
        }

        if (rulesMakingIt.Count > 0)
        {
            results.Add(RuleResult.Warning(Code,
                $"{Rules(rulesMakingIt, capital: true)} still {(rulesMakingIt.Count == 1 ? "makes" : "make")} {productCode}. Update or deactivate {Them(rulesMakingIt)} on Modification Rules."));
        }

        if (unitsInInventory > 0)
        {
            results.Add(RuleResult.Warning(Code,
                $"{unitsInInventory} unit{(unitsInInventory == 1 ? "" : "s")} of {productCode} {(unitsInInventory == 1 ? "is" : "are")} still in inventory; {(unitsInInventory == 1 ? "it keeps" : "they keep")} this product."));
        }

        if (minimumStockLevels > 0)
        {
            results.Add(RuleResult.Warning(Code,
                $"{productCode} still has {minimumStockLevels} minimum stock level{(minimumStockLevels == 1 ? "" : "s")}; remove {(minimumStockLevels == 1 ? "it" : "them")} on Minimum stock so a shortage is not reported."));
        }

        return results;
    }

    private static string Rules(IReadOnlyList<string> codes, bool capital = false) =>
        $"{(capital ? "M" : "m")}odification rule{(codes.Count == 1 ? "" : "s")} {string.Join(", ", codes.OrderBy(c => c))}";

    private static string Them(IReadOnlyList<string> codes) => codes.Count == 1 ? "it" : "them";
}
