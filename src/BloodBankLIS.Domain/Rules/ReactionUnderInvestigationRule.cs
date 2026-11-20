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

    /// <summary>The facility setting's default (OCD-078 as decided): a warning that needs an override.</summary>
    public const int DefaultAction = 1;

    /// <param name="action">Facility setting <c>Issue.ReactionUnderInvestigation</c>: 0 no check, 1 warning with override, 2 refused.</param>
    public static RuleResult Evaluate(long? investigationId, DateTime? reportedUtc, DateTime atUtc, int action = DefaultAction)
    {
        if (action <= 0 || investigationId is not { } id || reportedUtc is not { } reported || reported > atUtc)
        {
            return RuleResult.Pass(Code);
        }

        return action >= 2
            ? RuleResult.HardStop(Code,
                $"A transfusion reaction is under investigation for this patient (#{id}). No further unit is issued or started until the investigation is closed (facility setting, OCD-078).")
            : RuleResult.Warning(Code,
                $"A transfusion reaction is under investigation for this patient (#{id}). Continue only if the physician has decided to: give the reason and who authorized it (OCD-078).");
    }
}
