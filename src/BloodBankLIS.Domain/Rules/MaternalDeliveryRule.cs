namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A linked newborn whose delivery is not recorded on the mother's chart (end-user testing pass 146: MRN0028's infant
/// MRN0029 was born 2026-11-13 and linked to her chart, but no recent pregnancy was recorded, so her specimens would take
/// the standard window rather than the shorter window after a pregnancy in the look-back). The chart says so and offers
/// to record the delivery on the infant's date of birth; nothing is recorded by itself.
/// </summary>
public static class MaternalDeliveryRule
{
    /// <summary>
    /// The latest linked infant's date of birth within the look-back when no pregnancy on or after it is recorded; else
    /// null.
    /// </summary>
    public static DateOnly? Unrecorded(DateTime? recentPregnancyUtc, IEnumerable<DateOnly> infantBirthDates, DateOnly today,
        int lookbackDays = SpecimenValidityPolicy.DefaultLookbackDays)
    {
        var latest = infantBirthDates
            .Where(d => d <= today && today.DayNumber - d.DayNumber <= Math.Max(1, lookbackDays))
            .Select(d => (DateOnly?)d)
            .Max();
        if (latest is not DateOnly born)
        {
            return null;
        }

        var recorded = recentPregnancyUtc is DateTime pregnancy ? DateOnly.FromDateTime(pregnancy.ToLocalTime()) : (DateOnly?)null;
        // A pregnancy recorded the day before the birth date is the same delivery (labour, or a time recorded across
        // midnight or in another time zone).
        return recorded is DateOnly on && on >= born.AddDays(-1) ? null : born;
    }
}
