namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>
/// Patient age derived from date of birth. Neonatal rules depend on day-level
/// granularity, so days are exposed alongside months and years. A date of birth in
/// the future is a data error and yields a zero age rather than a negative one.
/// </summary>
public readonly record struct PatientAge(int Days, int Months, int Years)
{
    public static PatientAge Zero => new(0, 0, 0);

    public static PatientAge FromDateOfBirth(DateOnly dateOfBirth, DateTime asOfUtc)
    {
        var today = DateOnly.FromDateTime(asOfUtc);
        if (dateOfBirth >= today)
        {
            return Zero;
        }

        var days = today.DayNumber - dateOfBirth.DayNumber;

        var months = ((today.Year - dateOfBirth.Year) * 12) + today.Month - dateOfBirth.Month;
        if (today.Day < dateOfBirth.Day)
        {
            months--;
        }

        var years = months / 12;

        return new PatientAge(days, Math.Max(months, 0), Math.Max(years, 0));
    }
}
