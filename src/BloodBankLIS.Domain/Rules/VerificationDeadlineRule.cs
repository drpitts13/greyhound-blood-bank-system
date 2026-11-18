using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A correction of a verified result may be verified after the specimen expires (OCD-051), and so may a result entered
/// while the specimen was in date (OCD-093, decided 2026-11-17). This names when the tube expires for results still
/// waiting for verification, so they are verified while a repeat can still be run on it, and says which results the
/// expired-specimen check does not apply to.
/// </summary>
public static class VerificationDeadlineRule
{
    /// <summary>
    /// A pending correction (OCD-051), or an entered result waiting for verification that was entered while the specimen
    /// was in date (OCD-093), may be verified after the specimen expires.
    /// </summary>
    public static bool AllowsExpiredSpecimen(
        ResultStatus? status, DateTime? enteredUtc = null, DateTime? testedUtc = null, DateTime? specimenExpiresUtc = null) =>
        status == ResultStatus.Corrected
        || (status is ResultStatus.Entered or ResultStatus.PendingVerification
            && ExpiredSpecimenVerificationRule.EnteredWhileInDate(enteredUtc, testedUtc, specimenExpiresUtc));

    /// <summary>
    /// The notice for an entered or pending result whose specimen expires within
    /// <paramref name="window"/>; null otherwise (including once it has expired).
    /// </summary>
    public static string? Notice(ResultStatus? status, DateTime? specimenExpiresUtc, DateTime nowUtc, TimeSpan window)
    {
        if (status is not (ResultStatus.Entered or ResultStatus.PendingVerification)
            || specimenExpiresUtc is not DateTime expires
            || expires <= nowUtc
            || expires - nowUtc > window)
        {
            return null;
        }

        return $"Verify before {ClinicalTimeText.Local(expires)}, when the specimen expires, so a repeat can still be run on it. It can be verified after that (OCD-093), but nothing new can be tested on the tube.";
    }
}
