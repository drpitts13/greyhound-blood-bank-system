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
    public static bool OffersRoutineIssue(ReadyToIssueWorkItemDto row) =>
        !row.SpecimenBlocksIssue
        && !row.UnitOutdated
        && string.IsNullOrWhiteSpace(row.SpecialRequirementNotice)
        && !row.AboRhDiscrepancyBlocksIssue
        && !row.NeedsSecondAbo
        && !row.CrossmatchBlocksRoutineIssue;

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

    private static bool SameText(string? a, string? b) =>
        string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
}
