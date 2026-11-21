using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-042: eluate findings reach antibody history only through a second person's verification of a completed
/// DAT/elution workup, and only identified alloantibodies post. Autoantibody, panreactive, and nonspecific
/// findings never create antigen-negative requirements.
/// </summary>
public static class EluateVerificationRule
{
    public const string StatusCode = "ELU-VERIFY-STATUS";
    public const string SelfCode = "ELU-VERIFY-SELF";
    public const string FindingCode = "ELU-FINDING";

    public static RuleResult EvaluateVerify(DatElutionWorkupStatus status, string? completedBy, string verifier, bool alreadyVerified)
    {
        if (alreadyVerified)
        {
            return RuleResult.HardStop(StatusCode, "This DAT/elution workup is already verified.");
        }

        if (status != DatElutionWorkupStatus.Completed)
        {
            return RuleResult.HardStop(StatusCode, "Complete the DAT/elution workup before verifying it.");
        }

        return string.Equals(completedBy?.Trim(), verifier.Trim(), StringComparison.OrdinalIgnoreCase)
            ? RuleResult.HardStop(SelfCode, "A different person must verify the workup; you completed it.")
            : RuleResult.Pass(StatusCode);
    }

    /// <summary>Findings can change only before verification and while the workup is not voided.</summary>
    public static RuleResult EvaluateFindingsEditable(DatElutionWorkupStatus status, bool verified) =>
        verified
            ? RuleResult.HardStop(StatusCode, "Eluate findings cannot change after the workup is verified.")
            : status == DatElutionWorkupStatus.Voided
                ? RuleResult.HardStop(StatusCode, "A voided workup cannot record eluate findings.")
                : RuleResult.Pass(StatusCode);

    /// <summary>An alloantibody must name a catalog antibody so it can drive antigen-negative selection once posted.</summary>
    public static RuleResult EvaluateFinding(EluateFindingKind kind, string? specificity, bool catalogMatched)
    {
        if (string.IsNullOrWhiteSpace(specificity) && kind == EluateFindingKind.Alloantibody)
        {
            return RuleResult.HardStop(FindingCode, "Name the alloantibody found in the eluate.");
        }

        return kind == EluateFindingKind.Alloantibody && !catalogMatched
            ? RuleResult.HardStop(FindingCode, $"\"{specificity?.Trim()}\" is not a catalog antibody. Choose it from the antibody list so it can protect the patient once posted.")
            : RuleResult.Pass(FindingCode);
    }

    public static bool Posts(EluateFindingKind kind) => kind == EluateFindingKind.Alloantibody;

    /// <summary>A verified workup cannot be voided: its posted antibodies are retracted in antibody history instead.</summary>
    public static RuleResult EvaluateVoid(bool verified) =>
        verified
            ? RuleResult.HardStop(StatusCode, "A verified workup cannot be voided. Inactivate a posted antibody in the patient's antibody history, with a reason, if it was wrong.")
            : RuleResult.Pass(StatusCode);
}
