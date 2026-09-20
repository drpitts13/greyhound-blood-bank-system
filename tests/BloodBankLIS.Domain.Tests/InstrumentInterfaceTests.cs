using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Domain.Tests;

public class InstrumentInterfaceTests
{
    [Fact]
    public void MappingResolver_UsesOrderWhenMultipleApplicationCodesShareInstrumentCode()
    {
        var mappings = new[]
        {
            Map("Group", "ABORH-RETYPE", InstrumentMappingKind.Assay, 1),
            Map("Group", "ABORH", InstrumentMappingKind.Assay, 2)
        };

        Assert.Equal("ABORH", InstrumentMappingResolver.ToApplication(mappings, "Group", InstrumentMappingKind.Assay, ["ABORH"]));
        Assert.Equal("ABORH-RETYPE", InstrumentMappingResolver.ToApplication(mappings, "Group", InstrumentMappingKind.Assay, null));
    }

    [Fact]
    public void MappingResolver_CollapsesInboundAnalytesOntoOneApplicationCode()
    {
        var mappings = new[]
        {
            Map("Anti-D1", "Anti-D", InstrumentMappingKind.Analyte, 1),
            Map("Anti-D2", "Anti-D", InstrumentMappingKind.Analyte, 2)
        };

        Assert.Equal("Anti-D", InstrumentMappingResolver.ToApplication(mappings, "Anti-D1", InstrumentMappingKind.Analyte));
        Assert.Equal("Anti-D", InstrumentMappingResolver.ToApplication(mappings, "Anti-D2", InstrumentMappingKind.Analyte));
    }

    [Fact]
    public void RuleEngine_ConvertsNumericFortyToThreePlus()
    {
        var rules = new[]
        {
            new InstrumentInterfaceRule
            {
                Name = "40",
                ConditionExpression = "message.subtest_result >= 40 AND message.subtest_result < 80",
                ActionExpression = "set('subtest_result','3+')",
                SortOrder = 1,
                IsEnabled = true
            }
        };

        var (fields, trace) = InstrumentRuleEngine.Apply(
            new Dictionary<string, string?> { [InstrumentDataItemKeys.SubtestResult] = "40" },
            rules);

        Assert.Equal("3+", fields[InstrumentDataItemKeys.SubtestResult]);
        Assert.Single(trace);
    }

    [Fact]
    public void RuleEngine_LeavesReadablePlusGradeUnchanged()
    {
        var rules = new[]
        {
            new InstrumentInterfaceRule
            {
                Name = "40",
                ConditionExpression = "message.subtest_result >= 40 AND message.subtest_result < 80",
                ActionExpression = "set('subtest_result','3+')",
                SortOrder = 1,
                IsEnabled = true
            },
            new InstrumentInterfaceRule
            {
                Name = "1",
                ConditionExpression = "message.subtest_result >= 1 AND message.subtest_result < 20",
                ActionExpression = "set('subtest_result','1+')",
                SortOrder = 2,
                IsEnabled = true
            }
        };

        var (fields, trace) = InstrumentRuleEngine.Apply(
            new Dictionary<string, string?> { [InstrumentDataItemKeys.SubtestResult] = "4+" },
            rules);

        Assert.Equal("4+", fields[InstrumentDataItemKeys.SubtestResult]);
        Assert.Empty(trace);
    }

    [Fact]
    public void Validator_RejectsTwoOutboundMappingsForOneApplicationCode()
    {
        var entity = new InstrumentInterface
        {
            Name = "Echo",
            Connection = new InstrumentConnection { Port = 2576 }
        };
        var evaluation = InstrumentInterfaceValidator.Validate(
            entity,
            duplicateName: false,
            duplicatePort: false,
            fieldMappings: [new InstrumentFieldMapping { DataItemKey = "patient_id", AstmPath = "P.3" }],
            testMappings:
            [
                Map("Group", "ABORH", InstrumentMappingKind.Assay, 1, InterfaceTranslationDirection.Both),
                Map("Group2", "ABORH", InstrumentMappingKind.Assay, 2, InterfaceTranslationDirection.Both)
            ],
            rules: []);

        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == "INST-MAP-APP");
    }

    [Theory]
    [InlineData("Group", "Group", true)]
    [InlineData("group", "GROUP", true)]
    [InlineData("Anti-A", "Group", false)]
    [InlineData("", "Group", false)]
    [InlineData("  ", "Group", false)]
    public void Classifier_AnalyteEqualsAssay_MatchesAssayCode(string analyte, string assay, bool expected)
    {
        Assert.Equal(
            expected,
            InstrumentInterpretationClassifier.IsInterpretation(
                analyte,
                assay,
                InstrumentInterpretationMatch.AnalyteEqualsAssay,
                null));
    }

    [Fact]
    public void Classifier_AnalyteInList_MatchesAnyConfiguredCode()
    {
        const string codes = "INTERP, RESULT;\nGroup";

        Assert.True(InstrumentInterpretationClassifier.IsInterpretation(
            "INTERP", "ABORH", InstrumentInterpretationMatch.AnalyteInList, codes));
        Assert.True(InstrumentInterpretationClassifier.IsInterpretation(
            "result", "ABORH", InstrumentInterpretationMatch.AnalyteInList, codes));
        Assert.True(InstrumentInterpretationClassifier.IsInterpretation(
            "Group", "ABORH", InstrumentInterpretationMatch.AnalyteInList, codes));
        Assert.False(InstrumentInterpretationClassifier.IsInterpretation(
            "Anti-A", "ABORH", InstrumentInterpretationMatch.AnalyteInList, codes));
        Assert.False(InstrumentInterpretationClassifier.IsInterpretation(
            "", "ABORH", InstrumentInterpretationMatch.AnalyteInList, codes));
    }

    [Fact]
    public void Classifier_Never_IsNeverInterpretation()
    {
        Assert.False(InstrumentInterpretationClassifier.IsInterpretation(
            "Group", "Group", InstrumentInterpretationMatch.Never, null));
        Assert.False(InstrumentInterpretationClassifier.IsInterpretation(
            "INTERP", "Group", InstrumentInterpretationMatch.Never, "INTERP"));
    }

    [Fact]
    public void Validator_RequiresCodesWhenMatchIsAnalyteInList()
    {
        var entity = new InstrumentInterface
        {
            Name = "Future",
            Connection = new InstrumentConnection { Port = 2578 },
            InterpretationMatch = InstrumentInterpretationMatch.AnalyteInList,
            InterpretationAnalyteCodes = "  "
        };
        var evaluation = InstrumentInterfaceValidator.Validate(
            entity,
            duplicateName: false,
            duplicatePort: false,
            fieldMappings: [new InstrumentFieldMapping { DataItemKey = "patient_id", AstmPath = "P.3" }],
            testMappings: [],
            rules: []);

        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == "INST-INTERP-CODES");
    }

    private static InstrumentTestMapping Map(
        string instrument,
        string application,
        InstrumentMappingKind kind,
        int sort,
        InterfaceTranslationDirection direction = InterfaceTranslationDirection.Inbound) =>
        new()
        {
            InstrumentCode = instrument,
            ApplicationCode = application,
            Kind = kind,
            Direction = direction,
            SortOrder = sort
        };
}
