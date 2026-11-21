namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A unit still out (issued, not transfused or returned) that was issued under an MRN other than the chart's current one,
/// as after an unidentified chart is merged into the identified chart. The patient's wristband may still carry the
/// earlier identity while the bedside check compares with the current MRN. Guidance only: no check changes (OCD-071).
/// </summary>
public static class IssuedUnderEarlierIdentityRule
{
    public const string Code = "ISS-EARLIER-ID";

    public static bool Applies(string? identifierAtIssue, string currentMrn) =>
        !string.IsNullOrWhiteSpace(identifierAtIssue)
        && !string.Equals(identifierAtIssue.Trim(), currentMrn.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>The MRN the unit was issued under when it differs from the current one; null otherwise.</summary>
    public static string? EarlierMrn(string? identifierAtIssue, string? currentMrn) =>
        currentMrn is not null && Applies(identifierAtIssue, currentMrn) ? identifierAtIssue!.Trim() : null;

    /// <summary>The note on the ward and bedside forms for a unit issued under an earlier MRN.</summary>
    public static string? BedsideNotice(string? earlierMrn, string? currentMrn) =>
        string.IsNullOrWhiteSpace(earlierMrn)
            ? null
            : $"Issued under {earlierMrn}; the patient is now MRN {currentMrn}. The wristband or unit tag may still show {earlierMrn}: make sure they agree before transfusing.";

    public static RuleResult Warning(int units, IEnumerable<string> earlierIdentities, string currentMrn)
    {
        var earlier = string.Join(", ", earlierIdentities.Distinct(StringComparer.OrdinalIgnoreCase));
        return RuleResult.Warning(Code,
            $"{units} {(units == 1 ? "unit is" : "units are")} still out under {earlier}. The patient's wristband may still show {earlier}; "
            + $"the bedside check now uses MRN {currentMrn}. Make sure the wristband and the unit tags agree before transfusing.");
    }
}
