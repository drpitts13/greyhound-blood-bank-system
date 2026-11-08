using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// An unidentified emergency patient (trauma, unconscious, no identification) registered so testing and emergency release
/// can start. The LIS gives a temporary MRN and a name that cannot be mistaken for a real one, both carrying the date and
/// a sequence number. Sex and an estimated age are required: the product rules for patients of childbearing potential read
/// them, and an unknown sex is treated as not male. The naming convention is the facility's to confirm (OCD-071).
/// </summary>
public static class UnidentifiedPatientRule
{
    public const string Code = "PAT-UNIDENTIFIED";
    public const string MrnPrefix = "UNK-";
    public const string LastName = "UNIDENTIFIED";
    public const int MaxEstimatedAgeYears = 120;

    /// <summary>The temporary MRN for the n-th unidentified patient registered on that (UTC) day: UNK-20261108-01.</summary>
    public static string Mrn(DateTime utc, int sequence) => $"{MrnPrefix}{DatePart(utc)}-{sequence:D2}";

    /// <summary>The MRNs of that day start with this, so the next sequence can be found.</summary>
    public static string DayPrefix(DateTime utc) => $"{MrnPrefix}{DatePart(utc)}-";

    /// <summary>First name: TRAUMA 20261108-01, matching the MRN so the wristband and chart agree.</summary>
    public static string FirstName(DateTime utc, int sequence) => $"TRAUMA {DatePart(utc)}-{sequence:D2}";

    /// <summary>A birth date that gives the estimated age today (1 January of that year).</summary>
    public static DateOnly EstimatedBirthDate(DateOnly today, int estimatedAgeYears) => new(today.Year - estimatedAgeYears, 1, 1);

    public static RuleResult Evaluate(Sex? sex, int? estimatedAgeYears)
    {
        if (sex is null)
        {
            return RuleResult.HardStop(Code, "Choose the patient's sex, or Unknown; an unknown sex is treated as not male for product selection.");
        }

        if (estimatedAgeYears is null)
        {
            return RuleResult.HardStop(Code, "Enter an estimated age in years; product selection for patients of childbearing potential uses it.");
        }

        return estimatedAgeYears is < 0 or > MaxEstimatedAgeYears
            ? RuleResult.HardStop(Code, $"The estimated age is 0 to {MaxEstimatedAgeYears} years.")
            : RuleResult.Pass(Code);
    }

    /// <summary>
    /// Merging the unidentified chart into the identified patient's chart: only an unidentified, unmerged chart, into
    /// a different active chart that is itself identified.
    /// </summary>
    public static RuleResult EvaluateIdentify(bool unidentified, PatientStatus status, long chartId, long? identifiedId, bool identifiedIsUnidentified, PatientStatus? identifiedStatus)
    {
        if (!unidentified)
        {
            return RuleResult.HardStop(Code, "This chart is not an unidentified patient; use Merge on the chart to keep.");
        }

        if (status == PatientStatus.Merged)
        {
            return RuleResult.HardStop(Code, "This unidentified chart was already merged into the identified patient's chart.");
        }

        if (identifiedId is null)
        {
            return RuleResult.HardStop(Code, "No patient has that MRN; register the identified patient first, or check the MRN.");
        }

        if (identifiedId == chartId)
        {
            return RuleResult.HardStop(Code, "Enter the identified patient's own MRN, not this chart's.");
        }

        if (identifiedIsUnidentified)
        {
            return RuleResult.HardStop(Code, "That MRN is another unidentified patient; enter the identified patient's MRN.");
        }

        return identifiedStatus == PatientStatus.Merged
            ? RuleResult.HardStop(Code, "That chart was merged into another; enter the MRN of the chart it was merged into.")
            : RuleResult.Pass(Code);
    }

    /// <summary>
    /// The issue type when an unidentified patient is chosen on the issue form: no tested specimen exists yet, so
    /// emergency release, unless massive transfusion was already chosen. Its own reason and authorizer are still required.
    /// </summary>
    public static IssueType IssueTypeFor(IssueType current) =>
        current is IssueType.EmergencyRelease or IssueType.MassiveTransfusion ? current : IssueType.EmergencyRelease;

    private static string DatePart(DateTime utc) => utc.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
}
