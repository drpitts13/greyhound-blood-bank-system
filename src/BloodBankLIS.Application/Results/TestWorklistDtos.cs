using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Results;

public enum TestWorklistFilter
{
    Pending,
    Completed,
    All
}

public sealed record TestWorkItemDto(
    long OrderLineId,
    long OrderId,
    long PatientId,
    string PatientName,
    string Mrn,
    string TestCode,
    string TestName,
    string OrderNumber,
    OrderPriority Priority,
    DateTime OrderedUtc,
    string? AccessionNumber,
    long? SpecimenId,
    SpecimenStatus? SpecimenStatus,
    DateTime? SpecimenExpiresUtc,
    bool SpecimenExpired,
    string? CurrentBloodType,
    bool HasAntibodyHistory,
    string? AntibodySummary,
    long? CurrentResultId,
    ResultStatus? CurrentResultStatus,
    string? CurrentResultValue,
    string? CurrentResultInterpretation,
    ResultSource? CurrentResultSource,
    bool CanEnterResults,
    string? BlockReason,
    bool ElectronicXmEligible = false,
    bool FacilityAllowsElectronicCrossmatch = false,
    string? ElectronicXmBlockReason = null,
    string? ElectronicXmClinicalBlockReason = null,
    bool HasOpenAntibodyIdWorkup = false,
    long? OpenAntibodyIdWorkupId = null,
    string? CurrentResultEnteredBy = null,
    bool AboRhDeltaHold = false,
    string? AboRhDeltaDetail = null,
    string? Comment = null,
    bool BlockAboSelfVerify = true,
    string? VerificationDeadlineNotice = null,
    // The tube's label barcode, so a bench scan of the label finds the line as well as its accession.
    string? SpecimenBarcode = null,
    // The line belongs to a transfusion reaction workup order: an investigation is waiting on it.
    bool ReactionWorkup = false,
    // Reagent lots recorded with the current result when it was entered by hand ("AHG AHG-24033").
    IReadOnlyList<string>? CurrentResultReagentLots = null,
    // A line added by a reflex rule: the verified result that added it ("ABSC Positive").
    string? ReflexFrom = null,
    // That result was later invalidated or corrected so it no longer matches: check whether the test is still needed.
    string? ReflexTriggerNotice = null,
    // Where the tube is stored (Store tubes), or null when it has not been put away.
    string? SpecimenStorageLocation = null)
{
    /// <summary>Where the investigation waiting on this line is worked (the board opened on this patient).</summary>
    public string ReactionHref => $"/reactions?patientId={PatientId}";

    public bool HasPostedInterfaceOrInstrumentValue =>
        CurrentResultStatus is ResultStatus.PendingVerification
        && CurrentResultSource is ResultSource.Interface or ResultSource.Instrument;

    public string BenchActionLabel =>
        CanEnterResults
            ? HasPostedInterfaceOrInstrumentValue || AwaitsVerification ? "Verify" : "Enter"
            : NeedsRecollection ? SpecimenRecollectionRule.BenchLabel : "Blocked";

    /// <summary>The line's tube was rejected or cancelled: a new specimen must be collected and linked.</summary>
    public bool NeedsRecollection => SpecimenRecollectionRule.NeedsRecollection(SpecimenStatus);

    /// <summary>A result is on the line and waiting for verification, whoever or whatever entered it.</summary>
    public bool AwaitsVerification =>
        CurrentResultId is > 0
        && CurrentResultStatus is ResultStatus.Entered or ResultStatus.PendingVerification or ResultStatus.Corrected;

    /// <summary>
    /// The bench button for this user: Review instead of Verify when this user entered the ABO/Rh and may not verify
    /// it themselves.
    /// </summary>
    public string BenchActionLabelFor(string? userName) =>
        BenchActionLabel == "Verify" && IsAboSelfVerifyBlocked(userName) ? "Review" : BenchActionLabel;

    /// <summary>Where the waiting value came from, for the verify prompt.</summary>
    public string AwaitingValueOrigin => CurrentResultSource switch
    {
        ResultSource.Interface => "The interface value",
        ResultSource.Instrument => "The instrument value",
        _ => string.IsNullOrWhiteSpace(CurrentResultEnteredBy)
            ? "The entered value"
            : $"The value entered by {CurrentResultEnteredBy}"
    };

    public bool IsAboSelfVerifyBlocked(string? userName) =>
        BlockAboSelfVerify
        && CurrentResultStatus is ResultStatus.Entered or ResultStatus.PendingVerification or ResultStatus.Corrected
        && string.Equals(TestCode, ResultService.AboRhTestCode, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(CurrentResultEnteredBy)
        && string.Equals(CurrentResultEnteredBy, userName, StringComparison.OrdinalIgnoreCase);

    public bool OffersAntibodyIdHandoff =>
        HasOpenAntibodyIdWorkup
        || string.Equals(TestCode, "ABSC", StringComparison.OrdinalIgnoreCase)
        || string.Equals(TestCode, "ABID", StringComparison.OrdinalIgnoreCase);

    public string AntibodyIdHref
    {
        get
        {
            if (OpenAntibodyIdWorkupId is long workupId)
            {
                return $"/patients/{PatientId}/antibody-id/{workupId}";
            }

            var href = $"/patients/{PatientId}/antibody-id";
            if (SpecimenId is > 0)
            {
                href += $"?specimenId={SpecimenId}";
            }

            return href;
        }
    }

    public string AntibodyIdHandoffLabel => HasOpenAntibodyIdWorkup ? "ABID" : "Start ABID";

    public bool NeedsSpecimenLink =>
        SpecimenId is null or 0
        || NeedsRecollection
        || BlockReason?.Contains("No specimen is linked", StringComparison.OrdinalIgnoreCase) == true;

    // The chart's accession form for a tube, which links the waiting orders it serves (this one among them). The
    // Orders tab's accession panel starts a new order instead, and saving it there would order the tests again.
    public string ChartAccessionHref => $"/patients/{PatientId}?panel=specimen";
}

/// <summary>Views over the bench list that do not change what is on it.</summary>
public static class TestWorklistView
{
    /// <summary>
    /// Lines this user can verify now: a result waiting for verification, entry not blocked, and not an ABO/Rh this
    /// user entered. Order is kept.
    /// </summary>
    public static IReadOnlyList<TestWorkItemDto> ToVerify(IEnumerable<TestWorkItemDto> items, string? userName) =>
        items.Where(i => i.AwaitsVerification && i.BenchActionLabelFor(userName) == "Verify").ToList();

    /// <summary>Lines whose tube was rejected or cancelled: a new specimen must be collected. Order is kept.</summary>
    public static IReadOnlyList<TestWorkItemDto> Recollect(IEnumerable<TestWorkItemDto> items) =>
        items.Where(i => i.NeedsRecollection).ToList();

    /// <summary>
    /// Lines with no tube linked yet (not a rejected one: that is Recollect): the specimen still has to be collected or
    /// accessioned. Rush priorities first, then oldest order first.
    /// </summary>
    public static IReadOnlyList<TestWorkItemDto> NoSpecimen(IEnumerable<TestWorkItemDto> items) =>
        items.Where(i => i.SpecimenId is null or 0 && !i.NeedsRecollection)
            .OrderBy(i => IsRush(i.Priority) ? 0 : 1)
            .ThenBy(i => i.OrderedUtc)
            .ToList();

    /// <summary>
    /// Reflex lines in <paramref name="after"/> that were not in <paramref name="before"/>, as one note for the bench:
    /// "Reflex added: ABID on ORD-1 (from ABSC Positive)." Null when none.
    /// </summary>
    public static string? NewReflexNote(IEnumerable<TestWorkItemDto> before, IEnumerable<TestWorkItemDto> after)
    {
        var known = before.Select(i => i.OrderLineId).ToHashSet();
        var added = after
            .Where(i => i.ReflexFrom is not null && !known.Contains(i.OrderLineId))
            .Select(i => $"{i.TestCode} on {i.OrderNumber} (from {i.ReflexFrom})")
            .ToList();
        return added.Count == 0 ? null : $"Reflex added: {string.Join("; ", added)}.";
    }

    /// <summary>"ABSC Positive" for a reflex line; null for any other line.</summary>
    public static string? ReflexLabel(string? fromTestCode, string? fromValue) =>
        string.IsNullOrWhiteSpace(fromTestCode)
            ? null
            : string.IsNullOrWhiteSpace(fromValue) ? fromTestCode.Trim() : $"{fromTestCode.Trim()} {fromValue.Trim()}";

    /// <summary>
    /// After a save: the tube of the line just saved when it now has no line left on the worklist and has not been put
    /// away, so the bench can store it; null otherwise.
    /// </summary>
    public static string? TubeFinished(TestWorkItemDto? saved, IEnumerable<TestWorkItemDto> after)
    {
        if (saved is null || string.IsNullOrWhiteSpace(saved.AccessionNumber) || !string.IsNullOrWhiteSpace(saved.SpecimenStorageLocation))
        {
            return null;
        }

        return ForTube(after, saved.AccessionNumber).Count == 0 ? saved.AccessionNumber : null;
    }

    /// <summary>Orders (not lines) at a rush priority that have no tube yet.</summary>
    public static int RushOrdersWithoutSpecimen(IEnumerable<TestWorkItemDto> items) =>
        NoSpecimen(items).Where(i => IsRush(i.Priority)).Select(i => i.OrderId).Distinct().Count();

    /// <summary>The priorities the bench marks urgent.</summary>
    public static bool IsRush(OrderPriority priority) =>
        priority is OrderPriority.Stat or OrderPriority.Urgent or OrderPriority.EmergencyRelease or OrderPriority.MassiveTransfusionProtocol;

    /// <summary>The tube as scanned or typed, cleaned the same way as a receipt scan (scanner noise removed, trimmed, upper case).</summary>
    public static string NormalizeTube(string? scan) => SpecimenReceiptScanRule.Normalize(scan);

    /// <summary>
    /// Lines on the tube whose accession number or label barcode is the scan (exact, ignoring case and spaces), the same
    /// match as receiving a tube by scan. Order is kept.
    /// </summary>
    public static IReadOnlyList<TestWorkItemDto> ForTube(IEnumerable<TestWorkItemDto> items, string? scan)
    {
        var tube = NormalizeTube(scan);
        return tube.Length == 0
            ? items.ToList()
            : items.Where(i => SpecimenReceiptScanRule.Matches(tube, i.AccessionNumber ?? string.Empty, i.SpecimenBarcode)).ToList();
    }

    /// <summary>How the scanned tube is named on the bench: its accession when a line matched (a label scan), else the scan.</summary>
    public static string TubeLabel(IEnumerable<TestWorkItemDto> items, string? scan)
    {
        var tube = NormalizeTube(scan);
        return ForTube(items, tube).Select(i => i.AccessionNumber).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)) ?? tube;
    }

    /// <summary>The first line that can take a result now (not blocked, specimen linked), or null.</summary>
    public static TestWorkItemDto? FirstToWork(IEnumerable<TestWorkItemDto> items) =>
        items.FirstOrDefault(i => i is { CanEnterResults: true, NeedsSpecimenLink: false });
}
