using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Interprets a front-type-only product retype panel and compares it to the unit label.
/// Rh-positive units are confirmed for A and B only; Rh-negative units also require Anti-D.
/// </summary>
public static class AboRhRetypeRule
{
    public const string TestCode = "ABORH-RETYPE";
    public const string IncompleteCode = "RETYPE.SUBTEST.REQUIRED";
    public const string GradeCode = "RETYPE.SUBTEST.GRADE";
    public const string DiscrepancyCode = "RETYPE.LABEL.MISMATCH";

    public static AboRhRetypeOutcome Evaluate(
        AboGroup labeledAbo,
        RhType labeledRh,
        IReadOnlyDictionary<string, string> subtests)
    {
        ArgumentNullException.ThrowIfNull(subtests);

        var results = new List<RuleResult>();
        var requireAntiD = labeledRh == RhType.Negative;

        if (!TryRequiredPolarity(subtests, AboRhPanelSubtestCodes.AntiA, "Anti-A", results, out var antiA))
        {
            return Incomplete(results);
        }

        if (!TryRequiredPolarity(subtests, AboRhPanelSubtestCodes.AntiB, "Anti-B", results, out var antiB))
        {
            return Incomplete(results);
        }

        ReactionPolarity? antiD = null;
        if (requireAntiD)
        {
            if (!TryRequiredPolarity(subtests, AboRhPanelSubtestCodes.AntiD, "Anti-D", results, out var requiredD))
            {
                return Incomplete(results);
            }

            antiD = requiredD;
        }
        else if (TryOptionalPolarity(subtests, AboRhPanelSubtestCodes.AntiD, out var optionalD))
        {
            antiD = optionalD;
        }

        var interpretedAbo = InterpretAbo(antiA, antiB);
        var interpretedRh = antiD switch
        {
            ReactionPolarity.Positive => RhType.Positive,
            ReactionPolarity.Negative => RhType.Negative,
            _ => (RhType?)null
        };

        var aboMatches = interpretedAbo != AboGroup.Unknown && interpretedAbo == labeledAbo;
        var rhMatches = !requireAntiD || interpretedRh == labeledRh;
        var matches = aboMatches && rhMatches;

        string? detail = null;
        if (!matches)
        {
            var typed = FormatType(interpretedAbo, requireAntiD ? interpretedRh : null);
            var labeled = FormatType(labeledAbo, labeledRh);
            detail = $"ABO/Rh retype discrepancy: labeled {labeled}, typed {typed}";
            results.Add(RuleResult.Warning(DiscrepancyCode, detail));
        }

        return new AboRhRetypeOutcome(
            new RuleEvaluation(results),
            interpretedAbo,
            interpretedRh,
            matches,
            detail);
    }

    private static AboRhRetypeOutcome Incomplete(List<RuleResult> results) =>
        new(new RuleEvaluation(results), AboGroup.Unknown, null, false, null);

    private static AboGroup InterpretAbo(ReactionPolarity antiA, ReactionPolarity antiB) =>
        (antiA, antiB) switch
        {
            (ReactionPolarity.Negative, ReactionPolarity.Negative) => AboGroup.O,
            (ReactionPolarity.Positive, ReactionPolarity.Negative) => AboGroup.A,
            (ReactionPolarity.Negative, ReactionPolarity.Positive) => AboGroup.B,
            (ReactionPolarity.Positive, ReactionPolarity.Positive) => AboGroup.AB,
            _ => AboGroup.Unknown
        };

    private static bool TryRequiredPolarity(
        IReadOnlyDictionary<string, string> subtests,
        string code,
        string label,
        List<RuleResult> results,
        out ReactionPolarity polarity)
    {
        polarity = ReactionPolarity.Neutral;
        if (!subtests.TryGetValue(code, out var grade) || string.IsNullOrWhiteSpace(grade))
        {
            results.Add(RuleResult.HardStop(IncompleteCode, $"Subtest '{label}' is required."));
            return false;
        }

        if (!TryPolarity(grade, out polarity))
        {
            results.Add(RuleResult.HardStop(GradeCode, $"Subtest '{label}' requires a valid reaction grade (not NT or mixed)."));
            return false;
        }

        return true;
    }

    private static bool TryOptionalPolarity(
        IReadOnlyDictionary<string, string> subtests,
        string code,
        out ReactionPolarity polarity)
    {
        polarity = ReactionPolarity.Neutral;
        return subtests.TryGetValue(code, out var grade)
            && !string.IsNullOrWhiteSpace(grade)
            && TryPolarity(grade, out polarity);
    }

    private static bool TryPolarity(string grade, out ReactionPolarity polarity)
    {
        polarity = ReactionPolarity.Neutral;
        if (!CellReactionGradeValue.TryParseCode(grade, out var parsed)
            || parsed is CellReactionGrade.NotTested or CellReactionGrade.Mixed)
        {
            return false;
        }

        polarity = parsed == CellReactionGrade.Zero ? ReactionPolarity.Negative : ReactionPolarity.Positive;
        return true;
    }

    public static string FormatType(AboGroup abo, RhType? rh)
    {
        var group = abo == AboGroup.Unknown ? "?" : abo.ToString();
        if (rh is null or RhType.Unknown)
        {
            return group;
        }

        return rh == RhType.Positive ? $"{group} Positive" : $"{group} Negative";
    }
}

public sealed record AboRhRetypeOutcome(
    RuleEvaluation Validation,
    AboGroup InterpretedAbo,
    RhType? InterpretedRh,
    bool MatchesLabel,
    string? DiscrepancyDetail)
{
    public bool CanRecord => !Validation.IsHardStopped;
}
