namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-078 (decided 2026-11-10, option C): while a transfusion reaction is under investigation for the patient, issuing
/// another unit, or starting one at the bedside, is a warning that needs an override: the reason and who authorized it
/// (the physician's decision to continue). It goes to supervisor review like any other override. A unit issued or
/// started before the reaction was reported is not affected.
/// </summary>
public static class ReactionUnderInvestigationRule
{
    public const string Code = "ISS-REACTION-OPEN";

    public static RuleResult Evaluate(long? investigationId, DateTime? reportedUtc, DateTime atUtc) =>
        investigationId is { } id && reportedUtc is { } reported && reported <= atUtc
            ? RuleResult.Warning(Code,
                $"A transfusion reaction is under investigation for this patient (#{id}). Continue only if the physician has decided to: give the reason and who authorized it (OCD-078).")
            : RuleResult.Pass(Code);
}
