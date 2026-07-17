using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>Validates ABORH panel subtest completeness and reaction grade codes.</summary>
public static class AboRhPanelValidator
{
    public static RuleEvaluation Validate(AboRhPanelResult panel) =>
        Validate(panel, null);

    public static RuleEvaluation Validate(
        AboRhPanelResult panel,
        IReadOnlyList<PanelSubtestDefinition>? configuredSubtests)
    {
        var results = new List<RuleResult>();

        if (panel.Abo == AboGroup.Unknown)
        {
            results.Add(RuleResult.HardStop("ABORH.ABO.REQUIRED", "Interpreted ABO group is required."));
        }

        if (panel.Rh == RhType.Unknown)
        {
            results.Add(RuleResult.HardStop("ABORH.RH.REQUIRED", "Interpreted Rh(D) is required."));
        }

        var subtests = configuredSubtests is { Count: > 0 }
            ? configuredSubtests
            : PanelSubtestDefinitions.DefaultAboRh().Select(s => s).ToList();

        foreach (var def in subtests.Where(s => s.Required))
        {
            if (!panel.Subtests.TryGetValue(def.Code, out var grade) || string.IsNullOrWhiteSpace(grade))
            {
                results.Add(RuleResult.HardStop("ABORH.SUBTEST.REQUIRED", $"Subtest '{def.Label}' ({def.Code}) is required."));
                continue;
            }

            if (!CellReactionGradeValue.TryParseCode(grade, out var parsed) || parsed == CellReactionGrade.NotTested)
            {
                results.Add(RuleResult.HardStop("ABORH.SUBTEST.GRADE", $"Subtest '{def.Label}' requires a valid reaction grade (not NT)."));
            }
        }

        foreach (var def in subtests.Where(s => !s.Required))
        {
            if (!panel.Subtests.TryGetValue(def.Code, out var grade) || string.IsNullOrWhiteSpace(grade))
            {
                continue;
            }

            if (!CellReactionGradeValue.TryParseCode(grade, out _))
            {
                results.Add(RuleResult.Warning("ABORH.SUBTEST.GRADE", $"Subtest '{def.Label}' has an unrecognized grade '{grade}'."));
            }
        }

        return new RuleEvaluation(results);
    }
}
