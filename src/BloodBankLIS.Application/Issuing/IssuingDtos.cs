using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

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
    string? ReceivedBy = null,
    string? CoolerId = null,
    UnitAppearance? Appearance = null,
    IReadOnlyList<string>? AcknowledgedSpecialRequirementCodes = null,
    /// <summary>Scanned P-tag ID. Required when the active P-tag layout places that field.</summary>
    string? PtagId = null,
    /// <summary>Required when IssuedUtc is earlier than now (late entry, for example after downtime).</summary>
    string? LateEntryReason = null,
    /// <summary>OCD-054: a paper issue record entered after downtime; needs issue.downtime-backentry.</summary>
    bool DowntimeBackEntry = false,
    /// <summary>The unit number typed from the paper record, in place of the fresh scan, for a downtime back-entry.</summary>
    string? BackEntryDin = null);

public sealed record ComponentScanVerificationRequest(
    string Din,
    string ProductCodeData,
    string? ExtendedDivisionCode,
    string AboRhdCode,
    string ExpirationEncoded);

public sealed record ReturnUnitRequest(
    string Reason,
    // Null means not answered: refused unless facility policy treats it as acceptable (OCD-040).
    bool? TemperatureAcceptable = null,
    bool? SealIntegrityAcceptable = null,
    bool? VisualInspectionAcceptable = null,
    bool TimeOutOfStorageAcceptable = true,
    UnitStatus? Destination = null);

public sealed record WardReceiptRequest(
    string ReceivedBy,
    bool VisualInspectionAcceptable = true,
    ComponentScanVerificationRequest? VerifiedScan = null,
    UnitAppearance? Appearance = null,
    /// <summary>OCD-054: for a downtime back-entered issue, the DIN on the paper record in place of the scan.</summary>
    string? BackEntryDin = null,
    /// <summary>With BackEntryDin: the receipt time written on the paper record (required).</summary>
    DateTime? ReceivedUtc = null);

/// <summary>Bedside start: who the patient is, the unit scan, and when it was spiked. Stop and outcome come later.</summary>
public sealed record StartTransfusionRequest(
    DateTime? StartUtc = null,
    string? Transfusionist = null,
    ComponentScanVerificationRequest? BedsideScan = null,
    string? SecondVerifier = null,
    string? Location = null,
    IdentityTokenType PatientIdentifier1Type = IdentityTokenType.MedicalRecordNumber,
    string? PatientIdentifier1Value = null,
    IdentityTokenType PatientIdentifier2Type = IdentityTokenType.DateOfBirth,
    string? PatientIdentifier2Value = null,
    /// <summary>OCD-054: for a downtime back-entered issue, the DIN on the paper record in place of the bedside scan.</summary>
    string? BackEntryDin = null);

/// <summary>Completes a started transfusion: when it stopped, how much was given, and the outcome.</summary>
public sealed record CompleteTransfusionRequest(
    TransfusionDisposition FinalDisposition,
    DateTime? StopUtc = null,
    decimal? VolumeTransfused = null,
    bool ReactionSuspected = false);

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
    IdentityTokenType PatientIdentifier1Type = IdentityTokenType.MedicalRecordNumber,
    string? PatientIdentifier1Value = null,
    IdentityTokenType PatientIdentifier2Type = IdentityTokenType.DateOfBirth,
    string? PatientIdentifier2Value = null,
    /// <summary>OCD-054: for a downtime back-entered issue, the DIN on the paper record in place of the bedside scan.</summary>
    string? BackEntryDin = null);

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
    string? WardReceivedBy = null,
    DateTime? RetrospectiveCrossmatchDueUtc = null,
    DateTime? RetrospectiveCrossmatchCompletedUtc = null,
    string? CoolerId = null,
    DateTime? InTransitDueUtc = null,
    UnitAppearance IssueAppearance = UnitAppearance.Acceptable,
    UnitAppearance WardAppearance = UnitAppearance.Acceptable,
    string? AcknowledgedSpecialRequirementCodes = null,
    string? LateEntryReason = null,
    DateTime? EnteredUtc = null,
    bool DowntimeBackEntry = false)
{
    public static IssueDto From(Issue i) => new(
        i.Id, i.AllocationId, i.BloodProductId, i.PatientId, i.IssuedTo, i.IssuedToLocation,
        i.IssuedUtc, i.IssuedBy, i.Comment, i.IssueType, i.OverrideId, i.Status, i.TestsIncompleteAtIssue,
        i.WardReceivedUtc, i.WardReceivedBy,
        i.RetrospectiveCrossmatchDueUtc, i.RetrospectiveCrossmatchCompletedUtc,
        i.CoolerId, i.InTransitDueUtc, i.IssueAppearance, i.WardAppearance,
        i.AcknowledgedSpecialRequirementCodes,
        i.LateEntryReason, i.LateEntryReason is null ? null : i.CreatedUtc, i.DowntimeBackEntry);
}

public sealed record InTransitWorkItemDto(
    long IssueId,
    long PatientId,
    string? MedicalRecordNumber,
    string? PatientDisplayName,
    long BloodUnitId,
    string? UnitNumber,
    string? CurrentBloodType,
    bool HasAntibodyHistory,
    string? AntibodySummary,
    DateTime? SpecimenExpiresUtc,
    bool SpecimenExpired,
    DateTime IssuedUtc,
    DateTime? DueUtc,
    bool IsOverdue,
    string? CoolerId,
    string? IssuedToLocation,
    IssueStatus Status,
    string? ProductName = null,
    DateTime? UnitExpiresUtc = null,
    string? UnitBloodType = null,
    string? SpecimenAccession = null,
    DateTime? CrossmatchExpiresUtc = null,
    CrossmatchResult? LatestCrossmatchResult = null,
    string? IssuedTo = null,
    IssueType IssueType = IssueType.Standard,
    string? IssuedBy = null,
    string? EmergencyReleaseDetails = null,
    string? SecondVerifier = null,
    string? EmergencyReleaseAuthorizer = null,
    UnitAppearance IssueAppearance = UnitAppearance.Acceptable,
    string? PtagCode = null,
    CrossmatchClinicalStatus CrossmatchStatus = CrossmatchClinicalStatus.NotPerformed,
    // OCD-054: custody was on paper, so the row asks for the receipt to be back-entered instead of showing overdue.
    bool DowntimeBackEntry = false)
{
    /// <summary>The MRN the unit was issued under when the chart has since changed MRN (a merge); null otherwise.</summary>
    public string? IssuedUnderEarlierMrn { get; init; }

    public static InTransitWorkItemDto From(
        Issue i,
        DateTime now,
        string? mrn,
        string? displayName,
        string? unitNumber,
        string? currentBloodType,
        bool hasAntibodyHistory,
        string? antibodySummary,
        DateTime? specimenExpiresUtc,
        bool specimenExpired,
        string? productName = null,
        DateTime? unitExpiresUtc = null,
        string? unitBloodType = null,
        string? specimenAccession = null,
        DateTime? crossmatchExpiresUtc = null,
        CrossmatchResult? latestCrossmatchResult = null,
        string? emergencyReleaseAuthorizer = null,
        IReadOnlyList<StorageLocationLabel.CatalogLocation>? issuePlaces = null) => new(
        i.Id, i.PatientId, mrn, displayName, i.BloodProductId, unitNumber, currentBloodType,
        hasAntibodyHistory, antibodySummary, specimenExpiresUtc, specimenExpired,
        i.IssuedUtc, i.InTransitDueUtc,
        !i.DowntimeBackEntry && InTransitPendingRule.EvaluateOverdue(i.InTransitDueUtc, now).Severity == RuleSeverity.Warning,
        i.CoolerId, IssueDestinationCue.Format(i.IssuedToLocation, issuePlaces), i.Status, productName, unitExpiresUtc, unitBloodType, specimenAccession, crossmatchExpiresUtc, latestCrossmatchResult,
        string.IsNullOrWhiteSpace(i.IssuedTo) ? null : i.IssuedTo.Trim(),
        i.IssueType,
        string.IsNullOrWhiteSpace(i.IssuedBy) ? null : i.IssuedBy.Trim(),
        string.IsNullOrWhiteSpace(i.EmergencyReleaseDetails) ? null : i.EmergencyReleaseDetails.Trim(),
        string.IsNullOrWhiteSpace(i.SecondVerifier) ? null : i.SecondVerifier.Trim(),
        string.IsNullOrWhiteSpace(emergencyReleaseAuthorizer) ? null : emergencyReleaseAuthorizer.Trim(),
        i.IssueAppearance,
        string.IsNullOrWhiteSpace(i.PtagCode) ? null : i.PtagCode.Trim(),
        i.CrossmatchStatus,
        i.DowntimeBackEntry)
    {
        IssuedUnderEarlierMrn = IssuedUnderEarlierIdentityRule.EarlierMrn(i.PatientIdentifier1, mrn)
    };
}

public sealed record OutstandingIssueWorkItemDto(
    long IssueId,
    long PatientId,
    string? MedicalRecordNumber,
    string? PatientDisplayName,
    string? DateOfBirth,
    long BloodUnitId,
    string? UnitNumber,
    string? CurrentBloodType,
    bool HasAntibodyHistory,
    string? AntibodySummary,
    DateTime? SpecimenExpiresUtc,
    bool SpecimenExpired,
    DateTime IssuedUtc,
    DateTime? WardReceivedUtc,
    string? WardReceivedBy,
    string? CoolerId,
    string? IssuedToLocation,
    IssueStatus Status,
    string? ProductName = null,
    DateTime? UnitExpiresUtc = null,
    string? UnitBloodType = null,
    string? SpecimenAccession = null,
    DateTime? CrossmatchExpiresUtc = null,
    CrossmatchResult? LatestCrossmatchResult = null,
    string? IssuedTo = null,
    IssueType IssueType = IssueType.Standard,
    string? IssuedBy = null,
    string? EmergencyReleaseDetails = null,
    string? SecondVerifier = null,
    string? EmergencyReleaseAuthorizer = null,
    UnitAppearance IssueAppearance = UnitAppearance.Acceptable,
    string? WardAppearance = null,
    string? PtagCode = null,
    CrossmatchClinicalStatus CrossmatchStatus = CrossmatchClinicalStatus.NotPerformed,
    bool StorageTimeOutGoverned = true,
    bool StorageTimeOutAcceptable = true,
    // Set when the transfusion was started at the bedside and is not yet completed.
    DateTime? TransfusionStartedUtc = null,
    // Started and still not completed after the facility completion reminder hours.
    bool TransfusionCompletionOverdue = false)
{
    /// <summary>The MRN the unit was issued under when the chart has since changed MRN (a merge); null otherwise.</summary>
    public string? IssuedUnderEarlierMrn { get; init; }

    public static OutstandingIssueWorkItemDto From(
        Issue i,
        DateTime now,
        string? mrn,
        string? displayName,
        string? dateOfBirth,
        string? unitNumber,
        string? currentBloodType,
        bool hasAntibodyHistory,
        string? antibodySummary,
        DateTime? specimenExpiresUtc,
        bool specimenExpired,
        string? productName = null,
        DateTime? unitExpiresUtc = null,
        string? unitBloodType = null,
        string? specimenAccession = null,
        DateTime? crossmatchExpiresUtc = null,
        CrossmatchResult? latestCrossmatchResult = null,
        string? emergencyReleaseAuthorizer = null,
        IReadOnlyList<StorageLocationLabel.CatalogLocation>? issuePlaces = null,
        bool storageTimeOutGoverned = true,
        bool storageTimeOutAcceptable = true) => new(
        i.Id, i.PatientId, mrn, displayName, dateOfBirth, i.BloodProductId, unitNumber, currentBloodType,
        hasAntibodyHistory, antibodySummary, specimenExpiresUtc, specimenExpired,
        i.IssuedUtc, i.WardReceivedUtc, i.WardReceivedBy, string.IsNullOrWhiteSpace(i.CoolerId) ? null : i.CoolerId.Trim(), IssueDestinationCue.Format(i.IssuedToLocation, issuePlaces), i.Status, productName, unitExpiresUtc, unitBloodType, specimenAccession, crossmatchExpiresUtc, latestCrossmatchResult,
        string.IsNullOrWhiteSpace(i.IssuedTo) ? null : i.IssuedTo.Trim(),
        i.IssueType,
        string.IsNullOrWhiteSpace(i.IssuedBy) ? null : i.IssuedBy.Trim(),
        string.IsNullOrWhiteSpace(i.EmergencyReleaseDetails) ? null : i.EmergencyReleaseDetails.Trim(),
        string.IsNullOrWhiteSpace(i.SecondVerifier) ? null : i.SecondVerifier.Trim(),
        string.IsNullOrWhiteSpace(emergencyReleaseAuthorizer) ? null : emergencyReleaseAuthorizer.Trim(),
        i.IssueAppearance,
        i.WardReceivedUtc is null ? null : i.WardAppearance.ToString(),
        string.IsNullOrWhiteSpace(i.PtagCode) ? null : i.PtagCode.Trim(),
        i.CrossmatchStatus,
        storageTimeOutGoverned,
        storageTimeOutAcceptable)
    {
        IssuedUnderEarlierMrn = IssuedUnderEarlierIdentityRule.EarlierMrn(i.PatientIdentifier1, mrn)
    };
}

public sealed record ReadyToIssueWorkItemDto(
    long AllocationId,
    long PatientId,
    string? MedicalRecordNumber,
    string? PatientDisplayName,
    string? DateOfBirth,
    long BloodUnitId,
    string? UnitNumber,
    string? CurrentBloodType,
    bool HasAntibodyHistory,
    string? AntibodySummary,
    DateTime? SpecimenExpiresUtc,
    bool SpecimenExpired,
    DateTime AllocatedUtc,
    ProductAllocationDisplayStatus DisplayStatus,
    CrossmatchResult? LatestCrossmatchResult,
    bool NeedsSecondAbo = false,
    bool SpecimenBlocksIssue = false,
    string? SpecimenBlockReason = null,
    bool UnitOutdated = false,
    string? UnitBlockReason = null,
    bool OpenAntibodyWorkup = false,
    string? OpenAntibodyWorkupNotice = null,
    bool ExpiresSoon = false,
    string? ElectronicCrossmatchNotice = null,
    bool CrossmatchBlocksRoutineIssue = false,
    string? CrossmatchNotice = null,
    string? SpecialRequirementNotice = null,
    string? AntigenNotice = null,
    bool AboRhDiscrepancyBlocksIssue = false,
    string? AboRhDiscrepancyNotice = null,
    string? ReactionNotice = null,
    string? DonationRestrictionNotice = null,
    string? ProductName = null,
    DateTime? UnitExpiresUtc = null,
    string? UnitBloodType = null,
    DateTime? CrossmatchExpiresUtc = null,
    string? StorageLocationName = null,
    string? SpecimenAccession = null,
    // OCD-053: no usable specimen, but this unit may be released as an emergency under override.
    bool PreSpecimenEmergencyAllowed = false,
    // When the reservation itself was set to end; past it, the unit is still held until someone releases it.
    DateTime? ReservationExpiresUtc = null,
    // The ward's open request to issue this unit now, and where to.
    DateTime? IssueRequestedUtc = null,
    string? IssueRequestedBy = null,
    string? IssueRequestedTo = null,
    // Where the patient's open visit says they are; a suggestion for the issue destination, never filled in alone.
    string? PatientVisitLocation = null);

public sealed record SecondAboWorkItemDto(
    long PatientId,
    string MedicalRecordNumber,
    string DisplayName,
    string CurrentAboRh,
    int ReservedUnitCount,
    string Reason);

public sealed record RetrospectiveCrossmatchWorkItemDto(
    long IssueId,
    long PatientId,
    string? MedicalRecordNumber,
    string? PatientDisplayName,
    long BloodUnitId,
    string? UnitNumber,
    string? CurrentBloodType,
    bool HasAntibodyHistory,
    string? AntibodySummary,
    DateTime? SpecimenExpiresUtc,
    bool SpecimenExpired,
    DateTime IssuedUtc,
    DateTime? DueUtc,
    bool IsOverdue,
    IssueType IssueType,
    IssueStatus Status,
    string? EmergencyReleaseDetails,
    long? SpecimenId = null,
    string? AccessionNumber = null,
    string? ProductName = null,
    DateTime? UnitExpiresUtc = null,
    string? UnitBloodType = null,
    DateTime? CrossmatchExpiresUtc = null,
    CrossmatchResult? LatestCrossmatchResult = null,
    string? IssuedTo = null,
    string? CoolerId = null,
    string? IssuedToLocation = null,
    string? IssuedBy = null,
    string? SecondVerifier = null,
    string? EmergencyReleaseAuthorizer = null,
    UnitAppearance IssueAppearance = UnitAppearance.Acceptable,
    string? PtagCode = null,
    CrossmatchClinicalStatus CrossmatchStatus = CrossmatchClinicalStatus.NotPerformed)
{
    public static RetrospectiveCrossmatchWorkItemDto From(
        Issue i,
        DateTime now,
        string? mrn,
        string? displayName,
        string? unitNumber,
        string? currentBloodType,
        bool hasAntibodyHistory,
        string? antibodySummary,
        DateTime? specimenExpiresUtc,
        bool specimenExpired,
        long? specimenId = null,
        string? accessionNumber = null,
        string? productName = null,
        DateTime? unitExpiresUtc = null,
        string? unitBloodType = null,
        DateTime? crossmatchExpiresUtc = null,
        CrossmatchResult? latestCrossmatchResult = null,
        string? emergencyReleaseAuthorizer = null,
        IReadOnlyList<StorageLocationLabel.CatalogLocation>? issuePlaces = null) => new(
        i.Id, i.PatientId, mrn, displayName, i.BloodProductId, unitNumber, currentBloodType,
        hasAntibodyHistory, antibodySummary, specimenExpiresUtc, specimenExpired,
        i.IssuedUtc, i.RetrospectiveCrossmatchDueUtc,
        i.RetrospectiveCrossmatchDueUtc is DateTime due && now > due,
        i.IssueType, i.Status, string.IsNullOrWhiteSpace(i.EmergencyReleaseDetails) ? null : i.EmergencyReleaseDetails.Trim(),
        specimenId, accessionNumber, productName, unitExpiresUtc, unitBloodType, crossmatchExpiresUtc, latestCrossmatchResult,
        string.IsNullOrWhiteSpace(i.IssuedTo) ? null : i.IssuedTo.Trim(),
        string.IsNullOrWhiteSpace(i.CoolerId) ? null : i.CoolerId.Trim(),
        IssueDestinationCue.Format(i.IssuedToLocation, issuePlaces),
        string.IsNullOrWhiteSpace(i.IssuedBy) ? null : i.IssuedBy.Trim(),
        string.IsNullOrWhiteSpace(i.SecondVerifier) ? null : i.SecondVerifier.Trim(),
        string.IsNullOrWhiteSpace(emergencyReleaseAuthorizer) ? null : emergencyReleaseAuthorizer.Trim(),
        i.IssueAppearance,
        string.IsNullOrWhiteSpace(i.PtagCode) ? null : i.PtagCode.Trim(),
        i.CrossmatchStatus);
}

public sealed record ReturnDto(
    long Id,
    long IssueId,
    long BloodUnitId,
    DateTime ReturnedUtc,
    string ReturnedBy,
    string Reason,
    bool ReissueEligible,
    // The unit went back to Received because its required retype is not confirmed (RetypeStockStatus).
    bool RetypePending = false,
    // The unit is back in available stock, so it can be reserved again (for the same patient or anyone).
    bool BackInStock = false,
    // The patient the unit had been issued to, when the issue was loaded with the return.
    long? PatientId = null)
{
    public static ReturnDto From(Return r) => new(
        r.Id, r.IssueId, r.BloodProductId, r.ReturnedUtc, r.ReturnedBy, r.Reason, r.ReissueEligible,
        r.ReissueEvaluationJson?.Contains("\"RetypePending\":true", StringComparison.Ordinal) == true,
        DestinationOf(r.ReissueEvaluationJson) == UnitStatus.Available,
        r.Issue?.PatientId);

    /// <summary>
    /// Opens Compatibility with the patient and unit filled in, so reserving the returned unit again for the
    /// same patient is one step and still runs every reservation check. Nothing is reserved automatically.
    /// </summary>
    public string? ReserveAgainHref =>
        BackInStock && PatientId is long patientId
            ? $"/compatibility?patientId={patientId}&bloodUnitId={BloodUnitId}&from=return"
            : null;

    private static UnitStatus? DestinationOf(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("destination", out var value) && value.TryGetInt32(out var number)
                ? (UnitStatus)number
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
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
