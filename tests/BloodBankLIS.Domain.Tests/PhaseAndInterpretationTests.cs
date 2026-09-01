using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class PhaseAndInterpretationTests
{
    private static readonly string[] Cells = ["Cell1", "Cell2", "Cell3"];
    private static readonly string[] InterpPhases = ["IS", "37C", "AHG"];

    [Fact]
    public void AssignmentParse_RoundTripsPhaseCodes()
    {
        var json = PanelSubtestAssignments.ToJson([
            new PanelSubtestAssignment("Cell1", true, 1, ["IS", "AHG", "CC"])
        ]);

        var parsed = PanelSubtestAssignments.Parse(json);
        Assert.Single(parsed);
        Assert.Equal(["IS", "AHG", "CC"], parsed[0].PhaseCodes);
    }

    [Fact]
    public void AssignmentParse_MissingPhaseCodes_IsBackwardCompatible()
    {
        var json = PanelSubtestAssignments.ToJson([new PanelSubtestAssignment("Anti-A", true, 1)]);
        var parsed = PanelSubtestAssignments.Parse(json);
        Assert.Single(parsed);
        Assert.True(parsed[0].PhaseCodes is null || parsed[0].PhaseCodes.Count == 0);
    }

    [Fact]
    public void PanelResultValue_PhasedRoundTrip_FlattensCompositeKeys()
    {
        var entered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PhaseResultKeys.Compose("Cell1", "IS")] = "0",
            [PhaseResultKeys.Compose("Cell1", "AHG")] = "2+",
            [PhaseResultKeys.Compose("Cell1", "CC")] = "3+"
        };

        var json = PanelResultValue.Format(entered);
        Assert.Contains("\"version\":2", json, StringComparison.Ordinal);
        Assert.True(PanelResultValue.TryParse(json, out var parsed));
        Assert.Equal("0", parsed[PhaseResultKeys.Compose("Cell1", "IS")]);
        Assert.Equal("2+", parsed[PhaseResultKeys.Compose("Cell1", "AHG")]);
        Assert.Equal("3+", parsed[PhaseResultKeys.Compose("Cell1", "CC")]);
    }

    [Fact]
    public void PanelResultValue_FlatV1_StillParses()
    {
        var json = """{"version":1,"subtests":{"IS":"0","AHG":"2+"}}""";
        Assert.True(PanelResultValue.TryParse(json, out var parsed));
        Assert.Equal("0", parsed["IS"]);
        Assert.Equal("2+", parsed["AHG"]);
    }

    [Fact]
    public void AllMatch_NegativeScreen_AllowsAllNeg()
    {
        var result = InterpretationLogicValidator.Validate(
            DefaultScreenLogic(), Catalog(), "Negative", AllNegative(), Phases());
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void AllMatch_NegativeScreen_HardStopsOnPositiveCell()
    {
        var entered = AllNegative();
        entered[PhaseResultKeys.Compose("Cell1", "AHG")] = "2+";
        var result = InterpretationLogicValidator.Validate(
            DefaultScreenLogic(), Catalog(), "Negative", entered, Phases());
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, h => h.Code == "INTERPRETATION.MISMATCH");
    }

    [Fact]
    public void AnyPositive_PositiveScreen_AllowsSinglePositivePhase()
    {
        var entered = AllNegative();
        entered[PhaseResultKeys.Compose("Cell2", "AHG")] = "1+";
        var result = InterpretationLogicValidator.Validate(
            DefaultScreenLogic(), Catalog(), "Positive", entered, Phases());
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void AnyPositive_PositiveScreen_HardStopsWhenAllNegative()
    {
        var result = InterpretationLogicValidator.Validate(
            DefaultScreenLogic(), Catalog(), "Positive", AllNegative(), Phases());
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, h => h.Code == "INTERPRETATION.MISMATCH");
    }

    [Fact]
    public void Interpretation_IgnoresCheckCellExpectations()
    {
        var logic = DefaultScreenLogic();
        var negative = logic[0];
        var expectations = new Dictionary<string, ReactionPolarity>(negative.SubtestExpectations, StringComparer.OrdinalIgnoreCase)
        {
            [PhaseResultKeys.Compose("Cell1", "CC")] = ReactionPolarity.Negative
        };
        var rows = new List<InterpretationLogicRow>
        {
            negative with { SubtestExpectations = expectations },
            logic[1]
        };

        var entered = AllNegative();
        entered[PhaseResultKeys.Compose("Cell1", "CC")] = "3+";
        var result = InterpretationLogicValidator.Validate(rows, Catalog(), "Negative", entered, Phases());
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void CheckCellQc_BlocksNegativeCcWhenAhgNegative()
    {
        var entered = AllNegative();
        entered[PhaseResultKeys.Compose("Cell1", "CC")] = "0";
        var result = CheckCellQcValidator.Validate(Assignments(), Phases(), Catalog(), entered);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, h => h.Code == "CHECKCELL.INVALID");
    }

    [Fact]
    public void CheckCellQc_AllowsBlankCcWhenAhgNegative()
    {
        var result = CheckCellQcValidator.Validate(Assignments(), Phases(), Catalog(), AllNegative());
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void CheckCellQc_AllowsPositiveCcWhenAhgNegative()
    {
        var entered = AllNegative();
        entered[PhaseResultKeys.Compose("Cell1", "CC")] = "3+";
        var result = CheckCellQcValidator.Validate(Assignments(), Phases(), Catalog(), entered);
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void TestDefinitionValidator_RejectsCheckCellInLogic()
    {
        var def = new TestDefinition
        {
            Code = "ABSC",
            Name = "Antibody Screen",
            ResultValueType = ResultValueType.Subtest,
            PanelSubtestsJson = PanelSubtestAssignments.ToJson(Assignments()),
            InterpretationLogicJson = InterpretationLogicDefinitions.ToJson([
                new InterpretationLogicRow("Negative", "Negative", new Dictionary<string, ReactionPolarity>
                {
                    [PhaseResultKeys.Compose("Cell1", "CC")] = ReactionPolarity.Positive
                })
            ])
        };

        var result = TestDefinitionValidator.Validate(
            def,
            duplicateActiveCode: false,
            activeSubtestCodes: new HashSet<string>(Cells, StringComparer.OrdinalIgnoreCase),
            phasesByCode: Phases());

        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, h => h.Code == "TESTDEF.LOGIC.PHASE.CHECKCELL");
    }

    [Fact]
    public void DropUnassignedPhaseExpectations_RemovesPhasesNoLongerOnThePanel()
    {
        var rows = DefaultScreenLogic();
        var ahgOnly = Cells.Select((c, i) => new PanelSubtestAssignment(c, true, i + 1, ["AHG"])).ToList();

        var pruned = InterpretationLogicDefinitions.DropUnassignedPhaseExpectations(rows, ahgOnly);

        Assert.Equal(2, pruned.Count);
        foreach (var row in pruned)
        {
            Assert.Equal(3, row.SubtestExpectations.Count);
            foreach (var cell in Cells)
            {
                Assert.True(row.SubtestExpectations.ContainsKey(PhaseResultKeys.Compose(cell, "AHG")));
                Assert.False(row.SubtestExpectations.ContainsKey(PhaseResultKeys.Compose(cell, "IS")));
                Assert.False(row.SubtestExpectations.ContainsKey(PhaseResultKeys.Compose(cell, "37C")));
            }
        }

        var def = new TestDefinition
        {
            Code = "ABSC",
            Name = "Antibody Screen",
            ResultValueType = ResultValueType.Subtest,
            PanelSubtestsJson = PanelSubtestAssignments.ToJson(ahgOnly),
            InterpretationLogicJson = InterpretationLogicDefinitions.ToJson(pruned)
        };
        var result = TestDefinitionValidator.Validate(
            def,
            duplicateActiveCode: false,
            activeSubtestCodes: new HashSet<string>(Cells, StringComparer.OrdinalIgnoreCase),
            phasesByCode: Phases());
        Assert.False(result.IsHardStopped);
    }

    [Fact]
    public void TestDefinitionValidator_RejectsUnknownPhase()
    {
        var def = new TestDefinition
        {
            Code = "ABSC",
            Name = "Antibody Screen",
            ResultValueType = ResultValueType.Subtest,
            PanelSubtestsJson = PanelSubtestAssignments.ToJson([
                new PanelSubtestAssignment("Cell1", true, 1, ["NOPE"])
            ])
        };

        var result = TestDefinitionValidator.Validate(
            def,
            duplicateActiveCode: false,
            activeSubtestCodes: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cell1" },
            phasesByCode: Phases());

        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, h => h.Code == "TESTDEF.PANEL.PHASE.MISSING");
    }

    [Fact]
    public void PanelPhaseEntryValidator_RequiresInterpretivePhases()
    {
        var entered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PhaseResultKeys.Compose("Cell1", "IS")] = "0"
        };
        var result = PanelPhaseEntryValidator.ValidateRequired(Assignments(), Phases(), entered);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, h => h.Code == "PANEL.PHASE.REQUIRED");
    }

    [Fact]
    public void LogicRow_ParseDefaultsMatchMode()
    {
        var json = """[{"interpretationKey":"O|Positive","label":"Type O Positive","subtestExpectations":{"Anti-A":"Negative"}}]""";
        var rows = InterpretationLogicDefinitions.Parse(json);
        Assert.Single(rows);
        Assert.Equal(InterpretationMatchMode.AllMatch, rows[0].MatchMode);
    }

    private static IReadOnlyList<InterpretationLogicRow> DefaultScreenLogic() =>
        InterpretationLogicDefinitions.DefaultAntibodyScreenLogic(Cells, InterpPhases);

    private static IReadOnlyList<PanelSubtestAssignment> Assignments() =>
        Cells.Select((c, i) => new PanelSubtestAssignment(c, true, i + 1, ["IS", "37C", "AHG", "CC"])).ToList();

    private static Dictionary<string, string> AllNegative()
    {
        var entered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in Cells)
        {
            foreach (var phase in InterpPhases)
            {
                entered[PhaseResultKeys.Compose(cell, phase)] = "0";
            }
        }

        return entered;
    }

    private static Dictionary<string, SubtestDefinition> Catalog()
    {
        var choices = SubtestChoiceDefinitions.ToJson(SubtestChoiceDefinitions.DefaultGradedReaction());
        return Cells.ToDictionary(
            c => c,
            c => new SubtestDefinition
            {
                Code = c,
                Name = c,
                ResultType = SubtestResultType.GradedReaction,
                ChoicesJson = choices,
                IsActive = true
            },
            StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, PhaseDefinition> Phases() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["IS"] = new PhaseDefinition { Code = "IS", Name = "Immediate spin", IncludeInInterpretation = true, SortOrder = 1 },
        ["37C"] = new PhaseDefinition { Code = "37C", Name = "37°C", IncludeInInterpretation = true, SortOrder = 2 },
        ["AHG"] = new PhaseDefinition { Code = "AHG", Name = "AHG", IncludeInInterpretation = true, SortOrder = 3 },
        ["CC"] = new PhaseDefinition
        {
            Code = "CC",
            Name = "Check cells",
            IncludeInInterpretation = false,
            IsCheckCell = true,
            ValidatesPhaseCode = "AHG",
            SortOrder = 4
        }
    };
}
