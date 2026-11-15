namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The bedside confirmation that the patient's consent to transfusion is on the chart (end-user testing pass 115: the
/// bedside checks covered identity, the unit and a second verifier, but consent was not part of them). Recorded whenever
/// it is given; required to start or document a transfusion only when the facility turns it on (shipped off: whether
/// and how consent is checked at the bedside is the facility's procedure). Transfusions reported by the EHR are not
/// affected.
/// </summary>
public static class TransfusionConsentRule
{
    public const string Code = "TXN-CONSENT";

    public static RuleResult Evaluate(bool required, bool? consentConfirmed) =>
        required && consentConfirmed != true
            ? RuleResult.HardStop(Code, "Confirm that the patient's consent to transfusion is on the chart before the transfusion is started.")
            : RuleResult.Pass(Code);
}
