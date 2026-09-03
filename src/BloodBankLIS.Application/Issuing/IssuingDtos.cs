using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Issuing;

/// <summary>
/// Request to issue a unit. Patient identifiers, product match, special requirements,
/// and ABO discrepancy are evaluated by the computer — not operator checkboxes.
/// </summary>
public sealed record IssueUnitRequest(
    long BloodUnitId,
    long PatientId,
    IdentityTokenType PatientIdentifier1Type = IdentityTokenType.MedicalRecordNumber,
    string? PatientIdentifier1Value = null,
    IdentityTokenType PatientIdentifier2Type = IdentityTokenType.DateOfBirth,
    string? PatientIdentifier2Value = null,
    string? IssuedTo = null,
    string? IssuedToLocation = null,
    IssueType IssueType = IssueType.Standard,
    bool VisualInspectionAcceptable = true,
    string? SecondVerifier = null,
    long? OrderId = null,
    string? OverrideReason = null,
    string? AuthorizedBy = null,
    DateTime? IssuedUtc = null,
    string? Comment = null,
    /// <summary>Fresh positive unit identity verification at issue (required when unit has ISBT identity).</summary>
    ComponentScanVerificationRequest? VerifiedScan = null,
    string? ReceivedBy = null);

public sealed record ComponentScanVerificationRequest(
    string Din,
    string ProductCodeData,
    string? ExtendedDivisionCode,
    string AboRhdCode,
    string ExpirationEncoded);

public sealed record ReturnUnitRequest(
    string Reason,
    bool TemperatureAcceptable = true,
    bool SealIntegrityAcceptable = true,
    bool VisualInspectionAcceptable = true,
    bool TimeOutOfStorageAcceptable = true);

public sealed record WardReceiptRequest(
    string ReceivedBy,
    bool VisualInspectionAcceptable = true);

public sealed record DocumentTransfusionRequest(
    TransfusionDisposition FinalDisposition,
    DateTime? StartUtc = null,
    DateTime? StopUtc = null,
    decimal? VolumeTransfused = null,
    string? Transfusionist = null,
    bool ReactionSuspected = false,
    ComponentScanVerificationRequest? BedsideScan = null,
    string? SecondVerifier = null,
    string? Location = null,
    string? PatientIdentificationMethod = null,
    string? UnitIdentificationMethod = null,
    bool PositivePatientIdentification = false);

public sealed record IssueDto(
    long Id,
    long? AllocationId,
    long BloodUnitId,
    long PatientId,
    string? IssuedTo,
    string? IssuedToLocation,
    DateTime IssuedUtc,
    string IssuedBy,
    string? Comment,
    IssueType IssueType,
    long? OverrideId,
    IssueStatus Status,
    bool TestsIncompleteAtIssue,
    DateTime? WardReceivedUtc = null,
    string? WardReceivedBy = null)
{
    public static IssueDto From(Issue i) => new(
        i.Id, i.AllocationId, i.BloodProductId, i.PatientId, i.IssuedTo, i.IssuedToLocation,
        i.IssuedUtc, i.IssuedBy, i.Comment, i.IssueType, i.OverrideId, i.Status, i.TestsIncompleteAtIssue,
        i.WardReceivedUtc, i.WardReceivedBy);
}

public sealed record ReturnDto(
    long Id,
    long IssueId,
    long BloodUnitId,
    DateTime ReturnedUtc,
    string ReturnedBy,
    string Reason,
    bool ReissueEligible)
{
    public static ReturnDto From(Return r) => new(
        r.Id, r.IssueId, r.BloodProductId, r.ReturnedUtc, r.ReturnedBy, r.Reason, r.ReissueEligible);
}

public sealed record TransfusionEventDto(
    long Id,
    long IssueId,
    long BloodUnitId,
    long PatientId,
    DateTime? StartUtc,
    DateTime? StopUtc,
    decimal? VolumeTransfused,
    string? Transfusionist,
    bool ReactionSuspected,
    TransfusionDisposition FinalDisposition,
    string DocumentedBy)
{
    public static TransfusionEventDto From(TransfusionEvent t) => new(
        t.Id, t.IssueId, t.BloodProductId, t.PatientId, t.StartUtc, t.StopUtc, t.VolumeTransfused,
        t.Transfusionist, t.ReactionSuspected, t.FinalDisposition, t.DocumentedBy);
}
