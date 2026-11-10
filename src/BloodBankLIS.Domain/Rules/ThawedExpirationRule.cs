namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Expiration of a product thawed in place: the product code's thawed shelf life after the thaw time, never later
/// than the frozen unit's own expiration. The shelf life per product code is facility configuration (OCD-068).
/// </summary>
public static class ThawedExpirationRule
{
    public const string ShelfLifeCode = "AUTOTHAW.SHELF-LIFE";
    public const int DefaultHours = 24;
    public const int MaxHours = 720;

    public static DateTime Compute(DateTime thawUtc, int shelfLifeHours, DateTime frozenExpiresUtc)
    {
        var thawed = DateTime.SpecifyKind(thawUtc.AddHours(shelfLifeHours), DateTimeKind.Utc);
        return thawed < frozenExpiresUtc ? thawed : DateTime.SpecifyKind(frozenExpiresUtc, DateTimeKind.Utc);
    }

    public static RuleResult EvaluateShelfLife(int hours) =>
        hours is >= 1 and <= MaxHours
            ? RuleResult.Pass(ShelfLifeCode)
            : RuleResult.HardStop(ShelfLifeCode, $"Thawed shelf life must be between 1 and {MaxHours} hours.");

    public const string NotSetCode = "AUTOTHAW.SHELF-LIFE-NOT-SET";

    /// <summary>
    /// OCD-068 (decided 2026-11-10): the shelf life for this thaw. A closed-system single unit uses the code's thawed
    /// shelf life; an open-system thaw or a pooled unit uses that setting, and when both apply the shorter. A setting
    /// that applies but is not set refuses the thaw rather than falling back to the longer closed-system time.
    /// </summary>
    public static (int? Hours, RuleResult Result) ShelfLifeFor(
        string productCode, int closedHours, int? openHours, int? pooledHours, bool openSystem, bool pooled)
    {
        if (openSystem && openHours is null)
        {
            return (null, RuleResult.HardStop(NotSetCode,
                $"No open-system thawed shelf life is set for {productCode}. Set it on Admin › Auto thaw products before thawing in an open system."));
        }

        if (pooled && pooledHours is null)
        {
            return (null, RuleResult.HardStop(NotSetCode,
                $"No pooled thawed shelf life is set for {productCode}. Set it on Admin › Auto thaw products before thawing a pooled unit."));
        }

        var applicable = new List<int>();
        if (openSystem)
        {
            applicable.Add(openHours!.Value);
        }

        if (pooled)
        {
            applicable.Add(pooledHours!.Value);
        }

        return (applicable.Count == 0 ? closedHours : applicable.Min(), RuleResult.Pass(NotSetCode));
    }

    /// <summary>An optional open-system or pooled shelf life: blank (not set) or 1 to MaxHours.</summary>
    public static RuleResult EvaluateOptionalShelfLife(int? hours, string which) =>
        hours is null || hours is >= 1 and <= MaxHours
            ? RuleResult.Pass(ShelfLifeCode)
            : RuleResult.HardStop(ShelfLifeCode, $"The {which} thawed shelf life must be between 1 and {MaxHours} hours, or left blank.");

    /// <summary>"24 hours", "4 hours", "5 days" (whole days from 48 hours).</summary>
    public static string Describe(int hours) =>
        hours >= 48 && hours % 24 == 0 ? $"{hours / 24} days" : hours == 1 ? "1 hour" : $"{hours} hours";
}
