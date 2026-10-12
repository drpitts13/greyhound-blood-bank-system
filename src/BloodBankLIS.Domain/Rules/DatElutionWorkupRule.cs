using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// DAT/elution workup gates. Completing a positive DAT requires an elution
/// result or a not-performed reason. The workup never identifies antibodies.
/// </summary>
public static class DatElutionWorkupRule
{
    public const string PermissionCode = "DAT-ELU-PERM";
    public const string StatusCode = "DAT-ELU-STATUS";
    public const string SpecimenCode = "DAT-ELU-SPEC";
    public const string DatCode = "DAT-ELU-DAT";
    public const string ElutionCode = "DAT-ELU-ELUTION";
    public const string VoidReasonCode = "DAT-ELU-VOID";

    public static RuleResult EvaluatePermission(bool hasImmunoRecord) =>
        hasImmunoRecord
            ? RuleResult.Pass(PermissionCode)
            : RuleResult.HardStop(
                PermissionCode,
                "Recording a DAT/elution workup requires the immuno.record permission.");

    public static RuleResult EvaluateEditable(DatElutionWorkupStatus status) =>
        status == DatElutionWorkupStatus.Open
            ? RuleResult.Pass(StatusCode)
            : RuleResult.HardStop(StatusCode, $"A workup with status {status} cannot be edited.");

    public static RuleResult EvaluateSpecimen(SpecimenStatus? specimenStatus, bool specimenLinked) =>
        !specimenLinked
            ? RuleResult.Pass(SpecimenCode)
            : specimenStatus == SpecimenStatus.Accepted
                ? RuleResult.Pass(SpecimenCode)
                : RuleResult.HardStop(
                    SpecimenCode,
                    "A linked specimen must be Accepted before a DAT/elution workup can use it.");

    public static RuleResult EvaluateComplete(DatWorkupResult datResult, string? elutionResult, string? elutionNotPerformedReason)
    {
        if (datResult == DatWorkupResult.NotRecorded)
        {
            return RuleResult.HardStop(DatCode, "Record the DAT result before completing the workup.");
        }

        if (datResult == DatWorkupResult.Positive
            && string.IsNullOrWhiteSpace(elutionResult)
            && string.IsNullOrWhiteSpace(elutionNotPerformedReason))
        {
            return RuleResult.HardStop(
                ElutionCode,
                "DAT is positive; record the elution result or document why elution was not performed.");
        }

        return RuleResult.Pass(ElutionCode);
    }

    public static RuleResult EvaluateVoidReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? RuleResult.HardStop(VoidReasonCode, "A reason is required to void a DAT/elution workup.")
            : RuleResult.Pass(VoidReasonCode);

    public static RuleResult EvaluateVoidStatus(DatElutionWorkupStatus status) =>
        status == DatElutionWorkupStatus.Voided
            ? RuleResult.HardStop(StatusCode, "This DAT/elution workup is already voided.")
            : RuleResult.Pass(StatusCode);
}
