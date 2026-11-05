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
    string? SpecimenBarcode = null)
{
    public bool HasPostedInterfaceOrInstrumentValue =>
        CurrentResultStatus is ResultStatus.PendingVerification
        && CurrentResultSource is ResultSource.Interface or ResultSource.Instrument;

    public string BenchActionLabel =>
        CanEnterResults
            ? HasPostedInterfaceOrInstrumentValue || AwaitsVerification ? "Verify" : "Enter"
            : "Blocked";

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
        || BlockReason?.Contains("No specimen is linked", StringComparison.OrdinalIgnoreCase) == true;

    public string ChartAccessionHref => $"/patients/{PatientId}?tab=orders&panel=accession";
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
