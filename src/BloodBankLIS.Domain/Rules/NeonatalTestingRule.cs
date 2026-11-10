namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-074 (decided 2026-11-10, option B): pretransfusion testing for infants under the facility's age limit (four months
/// is the usual limit; the facility sets the days), enforced only when the facility turns the neonatal rules on (shipped
/// off until the medical director confirms the details against the current AABB Standards). This part: one specimen
/// covers the admission. A specimen collected from an infant during a visit stays valid until the visit is discharged or
/// the infant reaches the age limit, whichever is first, and never ends earlier than the usual rules would make it.
/// Part two (Cycle 673): ABO/Rh is not repeated during the admission. The infant's current type, tested on that admission
/// specimen while it is still valid, stands for routine red-cell issue without a second determination. Electronic
/// crossmatch is not affected: it still needs two determinations.
/// </summary>
public static class NeonatalTestingRule
{
    public const int DefaultAgeLimitDays = 120;

    /// <summary>Under the age limit at that time (age in whole days from the date of birth).</summary>
    public static bool IsNeonate(DateOnly dateOfBirth, DateTime atUtc, int ageLimitDays)
    {
        var age = DateOnly.FromDateTime(atUtc).DayNumber - dateOfBirth.DayNumber;
        return age >= 0 && age < ageLimitDays;
    }

    /// <summary>The day the infant reaches the age limit (00:00 UTC), when the neonatal rules stop applying.</summary>
    public static DateTime AgeLimitReachedUtc(DateOnly dateOfBirth, int ageLimitDays) =>
        dateOfBirth.AddDays(ageLimitDays).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    /// <summary>
    /// The expiration of an infant's specimen collected during a visit: the end of the admission (discharge, or the age
    /// limit if sooner), but never earlier than <paramref name="standardExpiresUtc"/>. Null when the rule does not apply
    /// (turned off, not an infant, no visit, a visit already discharged at collection) or would not extend it.
    /// </summary>
    public static DateTime? AdmissionExpiresUtc(
        bool enabled, int ageLimitDays, DateOnly dateOfBirth, DateTime collectedUtc, bool onVisit, DateTime? dischargeUtc,
        DateTime standardExpiresUtc)
    {
        if (!enabled || !onVisit || !IsNeonate(dateOfBirth, collectedUtc, ageLimitDays)
            || dischargeUtc is { } discharged && discharged <= collectedUtc)
        {
            return null;
        }

        var limit = AgeLimitReachedUtc(dateOfBirth, ageLimitDays);
        var end = dischargeUtc is { } d && d < limit ? d : limit;
        return end > standardExpiresUtc ? end : null;
    }

    /// <summary>
    /// The infant's current ABO/Rh stands without a second determination: it was tested on an admission specimen
    /// (one made valid for the admission under the neonatal rules) that has not expired, so the admission is still running.
    /// </summary>
    public static bool AdmissionTypeStands(bool typedOnAdmissionSpecimen, DateTime? specimenExpiresUtc, DateTime nowUtc) =>
        typedOnAdmissionSpecimen && specimenExpiresUtc is { } expires && expires > nowUtc;

    public const string AdmissionTypeMessage =
        "Infant: the ABO/Rh tested on this admission's specimen stands without a second determination (OCD-074).";

    /// <summary>
    /// When the visit is discharged, an admission-long specimen ends at discharge, but not before its usual expiration.
    /// Null when nothing changes.
    /// </summary>
    public static DateTime? OnDischarge(DateTime currentExpiresUtc, DateTime standardExpiresUtc, DateTime dischargeUtc)
    {
        var next = dischargeUtc > standardExpiresUtc ? dischargeUtc : standardExpiresUtc;
        return next < currentExpiresUtc ? next : null;
    }
}
