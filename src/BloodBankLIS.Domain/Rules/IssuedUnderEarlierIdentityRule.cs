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

    public static RuleResult Warning(int units, IEnumerable<string> earlierIdentities, string currentMrn)
    {
        var earlier = string.Join(", ", earlierIdentities.Distinct(StringComparer.OrdinalIgnoreCase));
        return RuleResult.Warning(Code,
            $"{units} {(units == 1 ? "unit is" : "units are")} still out under {earlier}. The patient's wristband may still show {earlier}; "
            + $"the bedside check now uses MRN {currentMrn}. Make sure the wristband and the unit tags agree before transfusing.");
    }
}
