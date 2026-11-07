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

    /// <summary>"24 hours", "4 hours", "5 days" (whole days from 48 hours).</summary>
    public static string Describe(int hours) =>
        hours >= 48 && hours % 24 == 0 ? $"{hours / 24} days" : hours == 1 ? "1 hour" : $"{hours} hours";
}
