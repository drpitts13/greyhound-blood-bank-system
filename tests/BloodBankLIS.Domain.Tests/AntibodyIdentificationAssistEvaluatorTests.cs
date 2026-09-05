using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class AntibodyIdentificationAssistEvaluatorTests
{
    private static readonly AntibodyIdAntigenInfo Kell = new("K", "anti-K");
    private static readonly AntibodyIdAntigenInfo BigE = new("E", "anti-E");
    private static readonly AntibodyIdAntigenInfo BigC = new("C", "anti-C");

    [Fact]
    public void Assist_NeverClassifiesIdentified()
    {
        var result = AntibodyIdentificationAssistEvaluator.Evaluate(PerfectKellPanel());

        Assert.DoesNotContain(result.Findings, f => f.Classification == AntibodyIdClassification.Identified);
        Assert.Contains(result.Evaluation.Results, r => r.Code == AntibodyIdentificationInterpretationRule.AssistAdvisoryCode);
        var kell = Assert.Single(result.Findings, f => f.AttributeCode == "K");
        Assert.Equal(AntibodyIdClassification.Possible, kell.Classification);
    }

    [Fact]
    public void HomozygousNegative_RulesOutDosageSensitiveAntigen()
    {
        var cells = new[]
        {
            Cell("1", PanelCellRole.Panel, antigens: new() { ["C"] = AntigenExpression.Homozygous },
                reactions: Neg("AHG")),
            Cell("2", PanelCellRole.Panel, antigens: new() { ["C"] = AntigenExpression.Absent },
                reactions: Pos("AHG"))
        };

        var result = Evaluate(cells, [BigC]);
        var finding = Assert.Single(result.Findings, f => f.AttributeCode == "C");
        Assert.Equal(AntibodyIdClassification.Excluded, finding.Classification);
        Assert.Equal(1, finding.HomozygousExclusions);
    }

    [Fact]
    public void HeterozygousNegative_DoesNotRuleOutWhenDosageAware()
    {
        var cells = new[]
        {
            Cell("1", PanelCellRole.Panel, antigens: new() { ["C"] = AntigenExpression.Heterozygous },
                reactions: Neg("AHG")),
            Cell("2", PanelCellRole.Panel, antigens: new() { ["C"] = AntigenExpression.Heterozygous },
                reactions: Neg("AHG")),
            Cell("3", PanelCellRole.Panel, antigens: new() { ["C"] = AntigenExpression.Absent },
                reactions: Pos("AHG"))
        };

        var result = Evaluate(cells, [BigC]);
        var finding = Assert.Single(result.Findings, f => f.AttributeCode == "C");
        Assert.Equal(AntibodyIdClassification.CannotExclude, finding.Classification);
        Assert.Equal(0, finding.HomozygousExclusions);
        Assert.Equal(2, finding.HeterozygousExclusions);
        Assert.Contains(result.Evaluation.Warnings, w =>
            w.Code == AntibodyIdentificationAssistEvaluator.SelectedCellNeededCode);
    }

    [Fact]
    public void HeterozygousNegative_RulesOutWhenDosageAwareOff()
    {
        var policy = AntibodyIdentificationPolicy.Default with { DosageAware = false };
        var cells = new[]
        {
            Cell("1", PanelCellRole.Panel, antigens: new() { ["C"] = AntigenExpression.Heterozygous },
                reactions: Neg("AHG")),
            Cell("2", PanelCellRole.Panel, antigens: new() { ["C"] = AntigenExpression.Heterozygous },
                reactions: Neg("AHG"))
        };

        var result = Evaluate(cells, [BigC], policy: policy);
        var finding = Assert.Single(result.Findings, f => f.AttributeCode == "C");
        Assert.Equal(AntibodyIdClassification.Excluded, finding.Classification);
    }

    [Fact]
    public void Kell_NotDosageSensitive_RulesOutOnPresentNegativeCells()
    {
        var cells = new[]
        {
            Cell("1", PanelCellRole.Panel, antigens: new() { ["K"] = AntigenExpression.Present },
                reactions: Neg("AHG")),
            Cell("2", PanelCellRole.Panel, antigens: new() { ["K"] = AntigenExpression.Present },
                reactions: Neg("AHG"))
        };

        var result = Evaluate(cells, [Kell]);
        var finding = Assert.Single(result.Findings, f => f.AttributeCode == "K");
        Assert.Equal(AntibodyIdClassification.Excluded, finding.Classification);
    }

    [Fact]
    public void PatternConsistent_IsPossible_NotIdentified()
    {
        var result = AntibodyIdentificationAssistEvaluator.Evaluate(PerfectKellPanel());
        var kell = Assert.Single(result.Findings, f => f.AttributeCode == "K");
        Assert.Equal(AntibodyIdClassification.Possible, kell.Classification);
        Assert.Contains("assistance only", kell.Rationale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PositiveOnAntigenNegativeCell_IsNotSoleSpecificity()
    {
        var cells = new[]
        {
            Cell("1", PanelCellRole.Panel, antigens: new() { ["K"] = AntigenExpression.Present },
                reactions: Pos("AHG")),
            Cell("2", PanelCellRole.Panel, antigens: new() { ["K"] = AntigenExpression.Absent },
                reactions: Pos("AHG"))
        };

        var result = Evaluate(cells, [Kell]);
        var finding = Assert.Single(result.Findings, f => f.AttributeCode == "K");
        Assert.Equal(AntibodyIdClassification.Inconclusive, finding.Classification);
        Assert.True(finding.DiscordantPositives > 0);
    }

    [Fact]
    public void Autocontrol_IsNotUsedForRuleOut()
    {
        var cells = new[]
        {
            Cell("AC", PanelCellRole.Autocontrol, antigens: new() { ["K"] = AntigenExpression.Homozygous },
                reactions: Neg("AHG")),
            Cell("1", PanelCellRole.Panel, antigens: new() { ["K"] = AntigenExpression.Present },
                reactions: Pos("AHG"))
        };

        var result = Evaluate(cells, [Kell]);
        var finding = Assert.Single(result.Findings, f => f.AttributeCode == "K");
        Assert.NotEqual(AntibodyIdClassification.Excluded, finding.Classification);
        Assert.Equal(0, finding.HomozygousExclusions);
    }

    [Fact]
    public void AutocontrolPositive_WithoutDat_Warns()
    {
        var cells = new[]
        {
            Cell("AC", PanelCellRole.Autocontrol, antigens: new(), reactions: Pos("AHG")),
            Cell("1", PanelCellRole.Panel, antigens: new() { ["K"] = AntigenExpression.Present },
                reactions: Pos("AHG"))
        };

        var result = Evaluate(cells, [Kell], dat: AntibodyIdDatResult.NotPerformed);
        Assert.Contains(result.Evaluation.Warnings, w => w.Code == AntibodyIdentificationAssistEvaluator.AutocontrolPositiveCode);
        Assert.Contains(result.Evaluation.Warnings, w => w.Code == AntibodyIdentificationAssistEvaluator.DatIndicatedCode);
    }

    [Fact]
    public void PositiveDat_WarnsWithoutIdentifying()
    {
        var result = Evaluate(
            [Cell("1", PanelCellRole.Panel, antigens: new() { ["K"] = AntigenExpression.Present }, reactions: Pos("AHG"))],
            [Kell],
            dat: AntibodyIdDatResult.PositiveIgG);

        Assert.Contains(result.Evaluation.Warnings, w => w.Code == AntibodyIdentificationAssistEvaluator.DatPositiveCode);
        Assert.DoesNotContain(result.Findings, f => f.Classification == AntibodyIdClassification.Identified);
    }

    [Fact]
    public void PatientPhenotypePositive_DowngradesPossible()
    {
        var input = PerfectKellPanel() with
        {
            PatientAntigens = [new PatientAntigenSnapshot("K", AntigenResult.Positive, FromGenotype: false)]
        };

        var result = AntibodyIdentificationAssistEvaluator.Evaluate(input);
        var kell = Assert.Single(result.Findings, f => f.AttributeCode == "K");
        Assert.Equal(AntibodyIdClassification.Inconclusive, kell.Classification);
        Assert.Contains(result.Evaluation.Warnings, w => w.Code == AntibodyIdentificationAssistEvaluator.PhenotypeConflictCode);
    }

    [Fact]
    public void PredictedGenotypePositive_DowngradesPossible()
    {
        var input = PerfectKellPanel() with
        {
            PatientAntigens = [new PatientAntigenSnapshot("K", AntigenResult.Positive, FromGenotype: true)]
        };

        var result = AntibodyIdentificationAssistEvaluator.Evaluate(input);
        Assert.Contains(result.Evaluation.Warnings, w => w.Code == AntibodyIdentificationAssistEvaluator.GenotypeConflictCode);
    }

    [Fact]
    public void HistoricalAntibody_IsSurfacedAndNotRemovedWhenPanelWouldExclude()
    {
        var cells = new[]
        {
            Cell("1", PanelCellRole.Panel, antigens: new() { ["K"] = AntigenExpression.Present },
                reactions: Neg("AHG")),
            Cell("2", PanelCellRole.Panel, antigens: new() { ["K"] = AntigenExpression.Present },
                reactions: Neg("AHG"))
        };

        var result = Evaluate(
            cells,
            [Kell],
            history: [new HistoricalAntibodySnapshot("anti-K", "K", AntibodyStatus.Identified, IsActive: true)]);

        Assert.Contains(result.Findings, f => f.Classification == AntibodyIdClassification.Historical && f.Specificity == "anti-K");
        Assert.Contains(result.Findings, f => f.Classification == AntibodyIdClassification.Excluded && f.AttributeCode == "K");
        Assert.Contains(result.Evaluation.Warnings, w => w.Code == AntibodyIdentificationAssistEvaluator.HistoricalUndetectedCode);
    }

    [Fact]
    public void BigCAndLittleC_AreEvaluatedSeparately()
    {
        var cells = new[]
        {
            Cell("1", PanelCellRole.Panel,
                antigens: new() { ["C"] = AntigenExpression.Homozygous, ["c"] = AntigenExpression.Absent },
                reactions: Neg("AHG")),
            Cell("2", PanelCellRole.Panel,
                antigens: new() { ["C"] = AntigenExpression.Absent, ["c"] = AntigenExpression.Homozygous },
                reactions: Pos("AHG"))
        };

        var result = Evaluate(cells, [BigC, new AntibodyIdAntigenInfo("c", "anti-c")]);
        Assert.Equal(AntibodyIdClassification.Excluded, Assert.Single(result.Findings, f => f.AttributeCode == "C").Classification);
        Assert.NotEqual(AntibodyIdClassification.Excluded, Assert.Single(result.Findings, f => f.AttributeCode == "c").Classification);
    }

    [Fact]
    public void SelectedCells_ParticipateInRuleOut()
    {
        var cells = new[]
        {
            Cell("S1", PanelCellRole.Selected, antigens: new() { ["E"] = AntigenExpression.Homozygous },
                reactions: Neg("AHG"))
        };

        var result = Evaluate(cells, [BigE]);
        var finding = Assert.Single(result.Findings, f => f.AttributeCode == "E");
        Assert.Equal(AntibodyIdClassification.Excluded, finding.Classification);
    }

    [Fact]
    public void IncompleteReactions_Warn()
    {
        var cells = new[]
        {
            Cell("1", PanelCellRole.Panel, antigens: new() { ["K"] = AntigenExpression.Present },
                reactions: new Dictionary<string, ReactionGrade>())
        };

        var result = Evaluate(cells, [Kell]);
        Assert.Contains(result.Evaluation.Warnings, w => w.Code == AntibodyIdentificationAssistEvaluator.IncompleteReactionsCode);
    }

    private static AntibodyIdentificationAssistInput PerfectKellPanel()
    {
        var cells = new[]
        {
            Cell("1", PanelCellRole.Panel, antigens: new() { ["K"] = AntigenExpression.Present, ["E"] = AntigenExpression.Absent },
                reactions: Pos("AHG")),
            Cell("2", PanelCellRole.Panel, antigens: new() { ["K"] = AntigenExpression.Absent, ["E"] = AntigenExpression.Homozygous },
                reactions: Neg("AHG")),
            Cell("AC", PanelCellRole.Autocontrol, antigens: new(), reactions: Neg("AHG"))
        };
        return new AntibodyIdentificationAssistInput(
            cells,
            ["AHG"],
            [Kell, BigE],
            [],
            [],
            AntibodyIdDatResult.Negative,
            AntibodyIdentificationPolicy.Default);
    }

    private static AntibodyIdentificationAssistResult Evaluate(
        IReadOnlyList<AntibodyIdentificationCellSnapshot> cells,
        IReadOnlyList<AntibodyIdAntigenInfo> antigens,
        IReadOnlyList<HistoricalAntibodySnapshot>? history = null,
        AntibodyIdDatResult dat = AntibodyIdDatResult.Negative,
        AntibodyIdentificationPolicy? policy = null) =>
        AntibodyIdentificationAssistEvaluator.Evaluate(new AntibodyIdentificationAssistInput(
            cells,
            ["AHG"],
            antigens,
            [],
            history ?? [],
            dat,
            policy ?? AntibodyIdentificationPolicy.Default));

    private static AntibodyIdentificationCellSnapshot Cell(
        string number,
        PanelCellRole role,
        Dictionary<string, AntigenExpression> antigens,
        Dictionary<string, ReactionGrade> reactions) =>
        new(number, number, role, antigens, reactions);

    private static Dictionary<string, ReactionGrade> Pos(string phase) =>
        new() { [phase] = ReactionGrade.TwoPlus };

    private static Dictionary<string, ReactionGrade> Neg(string phase) =>
        new() { [phase] = ReactionGrade.Negative };
}
