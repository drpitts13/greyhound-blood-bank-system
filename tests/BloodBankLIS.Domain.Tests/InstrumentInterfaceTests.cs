using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Domain.Tests;

public class InstrumentInterfaceTests
{
    [Fact]
    public void Translator_UsesOrderWhenMultipleTestsShareInstrumentCode()
    {
        var translator = InstrumentValueTranslator.From(
        [
            Map(InstrumentTranslationKeys.ResultTestCode, "Group", "ABORH-RETYPE", 1),
            Map(InstrumentTranslationKeys.ResultTestCode, "Group", "ABORH", 2)
        ]);

        Assert.Equal("ABORH", translator.ToLocal(InstrumentTranslationKeys.ResultTestCode, "Group", orderedTests: ["ABORH"]));
        Assert.Equal("ABORH-RETYPE", translator.ToLocal(InstrumentTranslationKeys.ResultTestCode, "group"));
    }

    [Fact]
    public void Translator_CollapsesInboundAnalytesOntoOneSubtest()
    {
        var translator = InstrumentValueTranslator.From(
        [
            Map(InstrumentTranslationKeys.SubtestCode, "Anti-D1", "Anti-D", 1),
            Map(InstrumentTranslationKeys.SubtestCode, "Anti-D2", "Anti-D", 2)
        ]);

        Assert.Equal("Anti-D", translator.ToLocal(InstrumentTranslationKeys.SubtestCode, "Anti-D1"));
        Assert.Equal("Anti-D", translator.ToLocal(InstrumentTranslationKeys.SubtestCode, "Anti-D2"));
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
            translations:
            [
                Map(InstrumentTranslationKeys.OrderTestCode, "Group", "ABORH", 1, InterfaceTranslationDirection.Outbound),
                Map(InstrumentTranslationKeys.OrderTestCode, "Group2", "ABORH", 2, InterfaceTranslationDirection.Outbound)
            ],
            rules: []);

        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == "INST-XLAT-OUT");
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
            translations: [],
            rules: []);

        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == "INST-INTERP-CODES");
    }

    private static InstrumentValueTranslation Map(
        string key,
        string instrument,
        string local,
        int sort,
        InterfaceTranslationDirection direction = InterfaceTranslationDirection.Inbound,
        string? context = null) =>
        new()
        {
            DataItemKey = key,
            InstrumentValue = instrument,
            LocalValue = local,
            Context = context,
            Direction = direction,
            SortOrder = sort
        };
}
