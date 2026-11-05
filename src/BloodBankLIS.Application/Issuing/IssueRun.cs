namespace BloodBankLIS.Application.Issuing;

/// <summary>
/// Several units for one patient in a row (a cooler, an MTP round, a two-unit order), at issue and at ward receipt.
/// The forms keep what the units share; this decides which unit is offered next. It does not decide eligibility:
/// every unit is still scanned, inspected, and checked on its own.
/// </summary>
public static class IssueRun
{
    /// <summary>
    /// The Ready to issue board offers a routine Issue for this row (the same test the board uses for its button).
    /// </summary>
    public static bool OffersRoutineIssue(ReadyToIssueWorkItemDto row) => RoutineIssueBlocks(row).Count == 0;

    /// <summary>What keeps the row from routine issue, in the board's words; empty when it can be issued.</summary>
    public static IReadOnlyList<string> RoutineIssueBlocks(ReadyToIssueWorkItemDto row)
    {
        var blocks = new List<string>();
        if (row.SpecimenBlocksIssue) blocks.Add(row.SpecimenBlockReason ?? "The specimen does not allow issue.");
        if (row.UnitOutdated) blocks.Add(row.UnitBlockReason ?? "The unit is outdated.");
        if (!string.IsNullOrWhiteSpace(row.SpecialRequirementNotice)) blocks.Add(row.SpecialRequirementNotice!);
        if (row.AboRhDiscrepancyBlocksIssue) blocks.Add(row.AboRhDiscrepancyNotice ?? "An ABO/Rh discrepancy is unresolved.");
        if (row.NeedsSecondAbo) blocks.Add(ReadyToIssueFormCue.SecondAbo);
        if (row.CrossmatchBlocksRoutineIssue) blocks.Add(row.CrossmatchNotice ?? "A compatible, unexpired crossmatch is required.");
        return blocks;
    }

    /// <summary>
    /// The patient's other reserved units that can be issued routinely, oldest reservation first, leaving out the unit
    /// on the form and any already issued in this run.
    /// </summary>
    public static IReadOnlyList<ReadyToIssueWorkItemDto> NextForPatient(
        IEnumerable<ReadyToIssueWorkItemDto> ready,
        long patientId,
        long? unitOnForm,
        IReadOnlyCollection<long> issuedThisRun) =>
        ready.Where(r => r.PatientId == patientId
                && r.BloodUnitId != unitOnForm
                && !issuedThisRun.Contains(r.BloodUnitId)
                && OffersRoutineIssue(r))
            .OrderBy(r => r.AllocatedUtc)
            .ThenBy(r => r.UnitNumber, StringComparer.Ordinal)
            .ToList();

    /// <summary>The patient's other reserved units that are not ready, so the run says why they are not offered.</summary>
    public static int HeldBackForPatient(
        IEnumerable<ReadyToIssueWorkItemDto> ready,
        long patientId,
        long? unitOnForm,
        IReadOnlyCollection<long> issuedThisRun) =>
        ready.Count(r => r.PatientId == patientId
            && r.BloodUnitId != unitOnForm
            && !issuedThisRun.Contains(r.BloodUnitId)
            && !OffersRoutineIssue(r));

    /// <summary>
    /// Units that went out with the one just received and still wait for ward receipt: the same patient and the same
    /// cooler, or, when no cooler was recorded, the same patient and destination. Earliest issued first. Downtime
    /// back-entries are left to the paper-record path.
    /// </summary>
    public static IReadOnlyList<InTransitWorkItemDto> NextToReceive(
        IEnumerable<InTransitWorkItemDto> inTransit,
        long patientId,
        string? coolerId,
        string? destination,
        IReadOnlyCollection<long> receivedIssueIds) =>
        inTransit.Where(r => r.PatientId == patientId
                && !r.DowntimeBackEntry
                && !receivedIssueIds.Contains(r.IssueId)
                && (string.IsNullOrWhiteSpace(coolerId)
                    ? string.IsNullOrWhiteSpace(r.CoolerId) && SameText(r.IssuedToLocation, destination)
                    : SameText(r.CoolerId, coolerId)))
            .OrderBy(r => r.IssuedUtc)
            .ThenBy(r => r.UnitNumber, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The patient's other units at the bedside: received on the ward and not yet finished. Units already transfusing
    /// come first (they need completing), then the rest by issue time.
    /// </summary>
    public static IReadOnlyList<OutstandingIssueWorkItemDto> NextAtBedside(
        IEnumerable<OutstandingIssueWorkItemDto> outstanding,
        long patientId,
        long? issueOnForm,
        IReadOnlyCollection<long> handledIssueIds) =>
        outstanding.Where(r => r.PatientId == patientId
                && r.WardReceivedUtc is not null
                && r.IssueId != issueOnForm
                && !handledIssueIds.Contains(r.IssueId))
            .OrderBy(r => r.TransfusionStartedUtc is null ? 1 : 0)
            .ThenBy(r => r.IssuedUtc)
            .ThenBy(r => r.UnitNumber, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// A unit picked or scanned on the Issue form. When it is reserved for the patient on the form, or the form has no
    /// patient yet, the reservation fills the patient (<c>Fill</c>). When it is reserved for someone else, the form is
    /// left alone and <c>Notice</c> says whose it is; the issue gate refuses it anyway.
    /// </summary>
    public static (ReadyToIssueWorkItemDto? Fill, string? Notice) ForPickedUnit(
        IEnumerable<ReadyToIssueWorkItemDto> ready,
        long? unitId,
        long patientOnForm)
    {
        if (unitId is null || ready.FirstOrDefault(r => r.BloodUnitId == unitId) is not { } row)
        {
            return (null, null);
        }

        if (patientOnForm <= 0 || patientOnForm == row.PatientId)
        {
            return (row, null);
        }

        var who = string.IsNullOrWhiteSpace(row.PatientDisplayName) ? $"patient {row.PatientId}" : row.PatientDisplayName;
        var mrn = string.IsNullOrWhiteSpace(row.MedicalRecordNumber) ? string.Empty : $" ({row.MedicalRecordNumber})";
        return (null, $"Unit {row.UnitNumber} is reserved for {who}{mrn}, not the patient on this form.");
    }

    /// <summary>
    /// The next step for an issued unit: complete a transfusion already running (even one started without a recorded
    /// ward receipt), otherwise receive it on the ward, then start or document the transfusion.
    /// </summary>
    public static IssuedNextStep NextStepForIssued(OutstandingIssueWorkItemDto row) =>
        row.TransfusionStartedUtc is not null ? IssuedNextStep.CompleteTransfusion
        : row.WardReceivedUtc is null ? IssuedNextStep.ReceiveOnWard
        : IssuedNextStep.StartTransfusion;

    /// <summary>A unit can still be returned until its transfusion has started.</summary>
    public static bool CanReturn(OutstandingIssueWorkItemDto row) => row.TransfusionStartedUtc is null;

    /// <summary>
    /// The next step offered for a near-expiry unit: issue it when it is reserved and Ready to issue would offer it,
    /// or allocate it when it is unreserved and its product has open orders (compatibility is checked at allocation).
    /// </summary>
    public static NearExpiryNextStep ForNearExpiry(
        bool reserved,
        ReadyToIssueWorkItemDto? readyRow,
        int openOrdersForProduct) =>
        reserved
            ? readyRow is not null && OffersRoutineIssue(readyRow) ? NearExpiryNextStep.Issue : NearExpiryNextStep.None
            : openOrdersForProduct > 0 ? NearExpiryNextStep.Allocate : NearExpiryNextStep.None;

    private static bool SameText(string? a, string? b) =>
        string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
}

public enum IssuedNextStep
{
    ReceiveOnWard,
    StartTransfusion,
    CompleteTransfusion
}

public enum NearExpiryNextStep
{
    None,
    Issue,
    Allocate
}
