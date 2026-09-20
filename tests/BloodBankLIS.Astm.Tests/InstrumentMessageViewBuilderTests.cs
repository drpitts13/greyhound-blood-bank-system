using BloodBankLIS.Astm.Messaging;
using BloodBankLIS.Astm.Parsing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Astm.Tests;

public class InstrumentMessageViewBuilderTests
{
    private const string GroupScreen = """
        H|\^&|||ImmuLINK|||||LIS|||LIS2-A2|20120219164429
        P|1|1171984|||Patient^Test||19590422|M
        O|1|0651439A||^^^Group|R||||||||||Blood^Patient
        R|1|^^^Mono Ctrl|0^0^0|||||F||KimP||20120219164429|M0002
        R|2|^^^Anti-A|0^0^0|||||F||KimP||20120219164429|M0002
        R|3|^^^Anti-D1|2+^2+^50|||||F||KimP||20120219164429|M0002
        R|8|^^^Group|B Pos|||||F||KimP||20120219164429|M0002
        O|2|0651439A||^^^Screen|R||||||||||Blood^Patient
        R|1|^^^Screen 1|4+^4+^100|||||F||KimP||20120219164429|M0002
        R|5|^^^Screen|Positive|||||F||KimP||20120219164429|M0002
        L|1|N
        """;

    [Fact]
    public void FormatRaw_BreaksCarriageReturnRecordsOntoOwnLines()
    {
        var raw = AstmBuilder.Join("H|\\^&", "P|1|1171984", "L|1|N");
        var display = InstrumentMessageViewBuilder.FormatRaw(raw);

        Assert.DoesNotContain('\r', display);
        Assert.Equal("H|\\^&\nP|1|1171984\nL|1|N", display);
    }

    [Fact]
    public void Build_PresentsParseAndMappingTables()
    {
        var message = AstmParser.Parse(GroupScreen.ReplaceLineEndings("\r"));
        var view = InstrumentMessageViewBuilder.Build(message, EchoInterface());

        Assert.Contains("P|1|1171984|||Patient^Test||19590422|M", view.RawDisplay.Split('\n'));
        Assert.Equal(view.RawDisplay.Split('\n').Length, message.Records.Count);

        Assert.Contains(view.ParsedFields, r => r.DataItem == InstrumentDataItemKeys.PatientId && r.Value == "1171984");
        Assert.Contains(view.ParsedFields, r => r.DataItem == InstrumentDataItemKeys.PatientLname && r.Value == "Patient");
        Assert.Contains(view.ParsedFields, r => r.DataItem == InstrumentDataItemKeys.SpecimenId && r.Value == "0651439A");
        Assert.Contains(view.ParsedFields, r => r.DataItem == InstrumentDataItemKeys.TestCode && r.Value == "Group");
        Assert.Contains(view.ParsedFields, r => r.DataItem == InstrumentDataItemKeys.SubtestCode && r.Value == "Anti-D1");
        Assert.Contains(view.ParsedFields, r => r.DataItem == InstrumentDataItemKeys.SubtestResult && r.Value == "2+");
        Assert.Contains(view.ParsedFields, r => r.DataItem == InstrumentDataItemKeys.TestInterp && r.Value == "B Pos");

        Assert.Contains(view.MappedValues, r => r.InstrumentValue == "Group" && r.ApplicationValue == "ABORH");
        Assert.Contains(view.MappedValues, r => r.InstrumentValue == "Screen" && r.ApplicationValue == "ABSC");
        Assert.Contains(view.MappedValues, r => r.InstrumentValue == "Anti-D1" && r.ApplicationValue == AboRhPanelSubtestCodes.AntiD);
        Assert.Contains(view.MappedValues, r => r.InstrumentValue == "Mono Ctrl" && r.ApplicationValue == AboRhPanelSubtestCodes.Control);
        Assert.Contains(view.MappedValues, r => r.InstrumentValue == "Screen 1" && r.ApplicationValue == "Cell1");
        Assert.Contains(view.MappedValues, r => r.InstrumentValue == "B Pos" && r.ApplicationValue == "B|Positive");
        Assert.DoesNotContain(view.MappedValues, r => r.InstrumentValue is "2+" or "4+");
    }

    [Fact]
    public void Build_DefaultEqualsAssay_YieldsGroupInterpretation()
    {
        var message = AstmParser.Parse(GroupScreen.ReplaceLineEndings("\r"));
        var view = InstrumentMessageViewBuilder.Build(message, EchoInterface());

        Assert.Contains(view.ParsedFields, r => r.DataItem == InstrumentDataItemKeys.TestInterp && r.Value == "B Pos");
        Assert.Contains(view.MappedValues, r => r.InstrumentValue == "B Pos" && r.ApplicationValue == "B|Positive");
    }

    [Fact]
    public void Build_NeverMatch_DoesNotTreatGroupRecordAsInterpretation()
    {
        var message = AstmParser.Parse(GroupScreen.ReplaceLineEndings("\r"));
        var iface = EchoInterface();
        iface.InterpretationMatch = InstrumentInterpretationMatch.Never;
        var view = InstrumentMessageViewBuilder.Build(message, iface);

        Assert.DoesNotContain(view.ParsedFields, r => r.DataItem == InstrumentDataItemKeys.TestInterp);
        Assert.Contains(view.ParsedFields, r => r.DataItem == InstrumentDataItemKeys.SubtestResult && r.Value == "B Pos");
        Assert.DoesNotContain(view.MappedValues, r => r.InstrumentValue == "B Pos");
    }

    private static InstrumentInterface EchoInterface() =>
        new()
        {
            FieldMappings = InstrumentDataItemCatalog.All.Select(item => new InstrumentFieldMapping
            {
                DataItemKey = item.Key,
                AstmPath = item.DefaultAstmPath
            }).ToList(),
            TestMappings =
            [
                Map("Group", "ABORH", InstrumentMappingKind.Assay),
                Map("Screen", "ABSC", InstrumentMappingKind.Assay),
                Map("Anti-A", AboRhPanelSubtestCodes.AntiA, InstrumentMappingKind.Analyte),
                Map("Anti-D1", AboRhPanelSubtestCodes.AntiD, InstrumentMappingKind.Analyte),
                Map("Mono Ctrl", AboRhPanelSubtestCodes.Control, InstrumentMappingKind.Analyte),
                Map("Screen 1", "Cell1", InstrumentMappingKind.Analyte)
            ],
            Rules =
            [
                new InstrumentInterfaceRule
                {
                    Name = "Numeric 1 to 1+",
                    ConditionExpression = "message.subtest_result >= 1 AND message.subtest_result < 20",
                    ActionExpression = "set('subtest_result','1+')",
                    SortOrder = 13,
                    IsEnabled = true
                },
                new InstrumentInterfaceRule
                {
                    Name = "B Pos",
                    ConditionExpression = "message.test_interp = 'B Pos'",
                    ActionExpression = "set('test_interp','B|Positive')",
                    SortOrder = 30,
                    IsEnabled = true
                }
            ]
        };

    private static InstrumentTestMapping Map(string instrument, string application, InstrumentMappingKind kind) =>
        new()
        {
            InstrumentCode = instrument,
            ApplicationCode = application,
            Kind = kind,
            Direction = InterfaceTranslationDirection.Both
        };
}
