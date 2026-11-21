using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

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
    public const string GradesCode = "DAT-ELU-GRADES";
    public const string ControlCode = "DAT-ELU-CONTROL";

    /// <summary>
    /// The reagent grades against the DAT result: a Negative DAT with a reagent reacting, or a Positive DAT with
    /// reagents recorded and none reacting, does not match and is refused. A reacting control is a warning; what it
    /// means for the test is in the reagent's instructions. Nothing is required: grades may be left not tested.
    /// </summary>
    public static IReadOnlyList<RuleResult> EvaluateGrades(
        DatWorkupResult result, ReactionGrade polyspecific, ReactionGrade igG, ReactionGrade c3, ReactionGrade control)
    {
        var reagents = new[] { ("polyspecific", polyspecific), ("anti-IgG", igG), ("anti-C3", c3) };
        var reacting = reagents.Where(r => ReactionGradeInfo.IsPositive(r.Item2)).Select(r => $"{r.Item1} {GradeText(r.Item2)}").ToList();
        var recorded = reagents.Count(r => r.Item2 != ReactionGrade.NotTested);
        var results = new List<RuleResult>();
        if (result == DatWorkupResult.Negative && reacting.Count > 0)
        {
            results.Add(RuleResult.HardStop(GradesCode, $"The DAT is recorded Negative but {string.Join(", ", reacting)} reacted. Correct the result or the grades."));
        }
        else if (result == DatWorkupResult.Positive && recorded > 0 && reacting.Count == 0)
        {
            results.Add(RuleResult.HardStop(GradesCode, "The DAT is recorded Positive but no reagent recorded reacted. Correct the result or the grades."));
        }
        else
        {
            results.Add(RuleResult.Pass(GradesCode));
        }

        results.Add(ReactionGradeInfo.IsPositive(control)
            ? RuleResult.Warning(ControlCode, $"The control reacted ({GradeText(control)}). Check the reagent's instructions before reporting the DAT.")
            : RuleResult.Pass(ControlCode));
        return results;
    }

    /// <summary>The grades recorded, for the workup summary: "Polyspecific 2+ · IgG 2+ · C3 0 · Control 0".</summary>
    public static string? GradeSummary(ReactionGrade polyspecific, ReactionGrade igG, ReactionGrade c3, ReactionGrade control)
    {
        var parts = new[] { ("Polyspecific", polyspecific), ("IgG", igG), ("C3", c3), ("Control", control) }
            .Where(p => p.Item2 != ReactionGrade.NotTested)
            .Select(p => $"{p.Item1} {GradeText(p.Item2)}")
            .ToList();
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    public static string GradeText(ReactionGrade g) => g switch
    {
        ReactionGrade.Negative => "0",
        ReactionGrade.Weak => "+w",
        ReactionGrade.OnePlus => "1+",
        ReactionGrade.TwoPlus => "2+",
        ReactionGrade.ThreePlus => "3+",
        ReactionGrade.FourPlus => "4+",
        ReactionGrade.Microscopic => "m",
        ReactionGrade.Hemolysis => "H",
        ReactionGrade.MixedField => "mf",
        ReactionGrade.Invalid => "invalid",
        _ => "not tested"
    };

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
