namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The date of a recent pregnancy on the chart (end-user testing pass 138: the chart could only record "pregnancy in the
/// last 3 months" as the moment the button was pressed, so a delivery six weeks earlier was dated today and the
/// alloimmunization look-back counted from the wrong day, with no way to enter the real date or clear a mistake). The
/// date may be entered; it cannot be in the future or before the patient was born.
/// </summary>
public static class RecentPregnancyRule
{
    public const string Code = "PATIENT-PREGNANCY";

    public static RuleResult Evaluate(DateTime? pregnancyUtc, DateOnly dateOfBirth, DateTime nowUtc)
    {
        if (pregnancyUtc is not DateTime when)
        {
            return RuleResult.Pass(Code);
        }

        if (DateOnly.FromDateTime(when) > DateOnly.FromDateTime(nowUtc).AddDays(1))
        {
            return RuleResult.HardStop(Code, "The pregnancy date cannot be in the future.");
        }

        if (DateOnly.FromDateTime(when) < dateOfBirth)
        {
            return RuleResult.HardStop(Code, $"The pregnancy date cannot be before the patient's date of birth ({dateOfBirth:yyyy-MM-dd}).");
        }

        return RuleResult.Pass(Code);
    }

    /// <summary>
    /// The UTC time recorded for a pregnancy date chosen on screen: now for today (as before), else the start of that
    /// local day.
    /// </summary>
    public static DateTime ToUtc(DateOnly localDate, DateTime nowUtc)
    {
        var today = DateOnly.FromDateTime(nowUtc.ToLocalTime());
        return localDate >= today
            ? nowUtc
            : DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
    }
}
