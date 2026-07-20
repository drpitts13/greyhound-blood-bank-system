using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Patients with a current/historical positive antibody screen or antibody history
/// must use a complex crossmatch unless an authorized exception override is supplied
/// for a simple crossmatch.
/// </summary>
public static class AntibodyHistoryCrossmatchRule
{
    public const string RuleCode = "ALLOC-XM-AB-HISTORY";

    /// <param name="requiresComplexCrossmatch">
    /// True when the patient has a positive antibody screen (current or historical)
    /// and/or known antibody history.
    /// </param>
    public static RuleResult Evaluate(
        bool requiresComplexCrossmatch,
        ResultValueType selectedCrossmatchType,
        bool overrideAuthorized)
    {
        if (!requiresComplexCrossmatch || selectedCrossmatchType == ResultValueType.ComplexCrossmatch)
        {
            return RuleResult.Pass(RuleCode);
        }

        if (selectedCrossmatchType == ResultValueType.Crossmatch)
        {
            return overrideAuthorized
                ? RuleResult.Pass(RuleCode, "Simple crossmatch allowed with authorized override.")
                : RuleResult.Warning(
                    RuleCode,
                    "Patient has a positive antibody screen (current or historical) or antibody history; complex crossmatch is required unless overridden with a comment.");
        }

        return RuleResult.HardStop(
            RuleCode,
            "Selected test is not a crossmatch or complex crossmatch result type.");
    }
}
