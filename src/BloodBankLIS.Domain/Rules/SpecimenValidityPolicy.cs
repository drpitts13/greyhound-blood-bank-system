namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// AABB specimen validity: 3 calendar days (72 hours from collection) when the
/// patient was transfused or pregnant in the lookback window; otherwise a longer
/// configured standard window. Pure; hours come from facility policy.
/// </summary>
public static class SpecimenValidityPolicy
{
    public const int DefaultAlloimmunizationRiskHours = 72;
    public const int DefaultStandardHours = 168;
    public const int DefaultLookbackDays = 90;

    public static DateTime ComputeExpiresUtc(
        DateTime collectedUtc,
        bool alloimmunizationRisk,
        int alloimmunizationRiskHours = DefaultAlloimmunizationRiskHours,
        int standardHours = DefaultStandardHours)
    {
        var hours = alloimmunizationRisk ? alloimmunizationRiskHours : standardHours;
        if (hours <= 0)
        {
            hours = alloimmunizationRisk ? DefaultAlloimmunizationRiskHours : DefaultStandardHours;
        }

        return collectedUtc.AddHours(hours);
    }

    public static bool HasAlloimmunizationRisk(
        DateTime asOfUtc,
        DateTime? recentTransfusionUtc,
        DateTime? recentPregnancyUtc,
        int lookbackDays = DefaultLookbackDays)
    {
        var cutoff = asOfUtc.AddDays(-Math.Max(1, lookbackDays));
        return (recentTransfusionUtc is not null && recentTransfusionUtc.Value >= cutoff)
            || (recentPregnancyUtc is not null && recentPregnancyUtc.Value >= cutoff);
    }
}
