namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A transfusion reaction noticed after the transfusion was documented without one (a delayed hemolytic reaction, a
/// fever or breathing problem after the unit finished). It opens the same investigation a suspected reaction at the
/// bedside opens, so it is never left unreported because the transfusion record said "no reaction".
/// </summary>
public static class LateReactionReportRule
{
    public const string Code = "RXN-LATE-REPORT";

    public static RuleResult Evaluate(
        bool hasPermission,
        bool transfusionFound,
        long? existingInvestigationId,
        DateTime? transfusionStartUtc,
        DateTime? onsetUtc,
        string? description,
        DateTime nowUtc)
    {
        if (!hasPermission)
        {
            return RuleResult.HardStop(Code,
                "Reporting a transfusion reaction requires permission to document transfusions or to investigate reactions.");
        }

        if (!transfusionFound)
        {
            return RuleResult.HardStop(Code, "No transfusion is recorded for this issue. Document the transfusion first.");
        }

        if (existingInvestigationId is long id)
        {
            return RuleResult.HardStop(Code,
                $"Reaction investigation {id} is already open for this transfusion. Add these findings to it.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return RuleResult.HardStop(Code, "Describe the reaction: the signs and symptoms, and who reported them.");
        }

        if (onsetUtc is { } onset)
        {
            if (transfusionStartUtc is { } start && onset < start)
            {
                return RuleResult.HardStop(Code,
                    $"The onset is before the transfusion started at {start:yyyy-MM-dd HH:mm} UTC. Check the time.");
            }

            if (onset > nowUtc + IssueTimeRule.ClockAllowance)
            {
                return RuleResult.HardStop(Code, "The onset time is in the future. Check the time.");
            }
        }

        return RuleResult.Pass(Code);
    }
}
