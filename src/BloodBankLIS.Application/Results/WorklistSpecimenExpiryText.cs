using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Results;

/// <summary>
/// End-user testing pass 255: on the Test Worklist an expired specimen's date was shown only in red (ACC0014, expired
/// 2026-11-09; ACC-RXN-POST1, expired 2026-11-15 with its ABO/Rh still to enter), with nothing in words. The row now says
/// the specimen expired, and, when no result has been entered, that a new specimen is needed to test (entering on an
/// expired specimen is refused except as downtime back-entry). A result entered while the specimen was in date can still
/// be verified (OCD-093), so that row says only that it expired.
/// </summary>
public static class WorklistSpecimenExpiryText
{
    public const string Expired = "expired";

    public static string? Note(bool specimenExpired, ResultStatus? resultStatus) =>
        !specimenExpired ? null
        : resultStatus is null or ResultStatus.Pending or ResultStatus.Invalidated
            ? "Specimen expired: collect a new specimen to test."
            : null;
}
