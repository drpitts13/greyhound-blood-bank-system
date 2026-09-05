using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Allowed result-status transitions. Instrument and interface values land in
/// pending verification; verified rows are never overwritten in place.
/// </summary>
public static class ResultLifecycleRule
{
    public static ResultStatus InitialStatus(ResultSource source) =>
        source is ResultSource.Instrument or ResultSource.Interface
            ? ResultStatus.PendingVerification
            : ResultStatus.Entered;

    public static bool CanUpdateInPlace(ResultStatus status) =>
        status is ResultStatus.Entered or ResultStatus.PendingVerification or ResultStatus.Corrected;

    public static bool CanSubmitForVerification(ResultStatus status) =>
        status is ResultStatus.Entered;

    public static bool CanVerify(ResultStatus status) =>
        status is ResultStatus.Entered or ResultStatus.PendingVerification or ResultStatus.Corrected;

    public static bool CanCorrect(ResultStatus status) =>
        status is ResultStatus.Verified;

    public static bool CanInvalidate(ResultStatus status) =>
        status is not ResultStatus.Invalidated and not ResultStatus.Pending;

    public static bool RestoresPriorVerifiedOnInvalidate(ResultStatus status) =>
        status is ResultStatus.Corrected;

    public static bool CreatesNewVersionOnInvalidate(ResultStatus status) =>
        status is ResultStatus.Verified;

    /// <summary>
    /// Current clinical row: not superseded. After an unverified correction is
    /// invalidated, the correction is superseded by the restored verified row
    /// so only one current row remains (OCD-016).
    /// </summary>
    public static bool IsCurrentRow(long? supersededByResultId) =>
        supersededByResultId is null;
}
