using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A result entered on a specimen cannot be verified once the specimen expires, but a
/// correction of a verified result can (OCD-051). This names the deadline for results
/// still waiting for verification so they are verified in time, and says which results
/// the expired-specimen check does not apply to.
/// </summary>
public static class VerificationDeadlineRule
{
    /// <summary>A pending correction may be verified after the specimen expires (OCD-051).</summary>
    public static bool AllowsExpiredSpecimen(ResultStatus? status) =>
        status == ResultStatus.Corrected;

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

        return $"Verify before {expires:yyyy-MM-dd HH:mm} UTC, when the specimen expires. After that this result cannot be verified.";
    }
}
