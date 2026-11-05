namespace BloodBankLIS.Application.Issuing;

/// <summary>
/// Issuing several units to one patient in a row (a cooler, an MTP round, a two-unit order). The issue form keeps the
/// patient, identifiers, destination, and cooler between units; this decides which of that patient's reserved units
/// the form offers next. It does not decide eligibility: every unit still goes through the issue gate.
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
}
