using BloodBankLIS.Astm.Messaging;
using BloodBankLIS.Astm.Parsing;
using BloodBankLIS.Astm.Simulator;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;

namespace BloodBankLIS.Astm.Tests;

public class AstmParserTests
{
    private const string GroupScreen = """
        H|\^&|||ImmuLINK|||||LIS|||LIS2-A2|20120219164429
        P|1|1171984|||Patient^Test||19590422|M
        O|1|0651439A||^^^Group|R||||||||||Blood^Patient
        R|1|^^^Mono Ctrl|0^0^0|||||F||KimP||20120219164429|M0002
        R|2|^^^Anti-A|0^0^0|||||F||KimP||20120219164429|M0002
        R|8|^^^Group|B Pos|||||F||KimP||20120219164429|M0002
        O|2|0651439A||^^^Screen|R||||||||||Blood^Patient
        R|1|^^^Screen 1|4+^4+^100|||||F||KimP||20120219164429|M0002
        R|5|^^^Screen|Positive|||||F||KimP||20120219164429|M0002
        L|1|N
        """;

    private const string Query = """
        H|\^&|||ImmuLINK|||||LIS|||LIS2-A2|20120219140243
        Q|1|0651439A||ALL
        Q|2|R02460||ALL
        L|1|N
        """;

    private const string Crossmatch = """
        H|\^&|||ImmuLINK|||||LIS|||LIS2-A2|20120219165000
        P|1|1171984|||Patient^Test||19590422|M
        O|1|0651439A||^^^Crossmatch|R||||||||||Blood^Patient
        R|1|^^^Donor|R02460|||||F||KimP||20120219165000|M0002
        R|2|^^^IgG XM|0^0^0|||||F||KimP||20120219165000|M0002
        R|3|^^^Ind Ctrl|4+^4+^100|||||F||KimP||20120219165000|M0002
        R|4|^^^Crossmatch|IgG Comp (Check ABO Comp)|||||F||KimP||20120219165000|M0002
        L|1|N
        """;

    private const string Donor = """
        H|\^&|||ImmuLINK|||||LIS|||LIS2-A2|20120219165100
        P|1||R02460||Unit^Donor||20120101|U
        O|1|R02460||^^^Donor|R||||||||||Blood^Product
        R|1|^^^Anti-A|4+^4+^90|||||F||KimP||20120219165100|M0002
        R|8|^^^Donor|A Pos|||||F||KimP||20120219165100|M0002
        L|1|N
        """;

    [Fact]
    public void Parse_GroupScreenExample_ReadsAssayAndAnalyte()
    {
        var message = AstmParser.Parse(GroupScreen.ReplaceLineEndings("\r"));
        Assert.Equal(InstrumentMessageKind.Result, message.Kind);
        var group = message.Records.First(r => r.Type == 'O');
        Assert.Equal("Group", group.GetComponent(5, 4));
        Assert.Equal("0651439A", group.GetField(3));
        var interp = message.Records.First(r => r.Type == 'R' && r.GetComponent(3, 4) == "Group");
        Assert.Equal("B Pos", interp.GetField(4));
        Assert.Equal("M0002", interp.GetField(14));
    }

    [Fact]
    public void Parse_QueryExample_ReadsAccessions()
    {
        var message = AstmParser.Parse(Query.ReplaceLineEndings("\r"));
        Assert.Equal(InstrumentMessageKind.Query, message.Kind);
        var accessions = message.Records.Where(r => r.Type == 'Q').Select(r => r.GetField(3)).ToList();
        Assert.Equal(["0651439A", "R02460"], accessions);
    }

    [Fact]
    public void Parse_CrossmatchExample_ReadsDonorAndInterp()
    {
        var message = AstmParser.Parse(Crossmatch.ReplaceLineEndings("\r"));
        Assert.Equal(InstrumentMessageKind.Result, message.Kind);
        Assert.Equal("Crossmatch", message.Records.First(r => r.Type == 'O').GetComponent(5, 4));
        Assert.Equal("R02460", message.Records.First(r => r.GetComponent(3, 4) == "Donor").GetField(4));
        Assert.Equal("IgG Comp (Check ABO Comp)", message.Records.First(r => r.GetComponent(3, 4) == "Crossmatch").GetField(4));
    }

    [Fact]
    public void Parse_DonorExample_ReadsProductAssay()
    {
        var message = AstmParser.Parse(Donor.ReplaceLineEndings("\r"));
        var order = message.Records.First(r => r.Type == 'O');
        Assert.Equal("Donor", order.GetComponent(5, 4));
        Assert.Contains("Blood^Product", order.Raw, StringComparison.Ordinal);
        Assert.Equal("A Pos", message.Records.First(r => r.GetComponent(3, 4) == "Donor").GetField(4));
    }

    [Fact]
    public void FieldAccessor_ReadsEchoCatalogPaths()
    {
        var message = AstmParser.Parse(GroupScreen.ReplaceLineEndings("\r"));
        var paths = AstmFieldAccessor.Index(
        [
            new InstrumentFieldMapping { DataItemKey = "patient_id", AstmPath = "P.3" },
            new InstrumentFieldMapping { DataItemKey = "specimen_id", AstmPath = "O.3" },
            new InstrumentFieldMapping { DataItemKey = "test_code", AstmPath = "O.5.4" },
            new InstrumentFieldMapping { DataItemKey = "subtest_code", AstmPath = "R.3.4" },
            new InstrumentFieldMapping { DataItemKey = "subtest_result", AstmPath = "R.4.1" }
        ]);

        Assert.Equal("1171984", AstmFieldAccessor.Read(paths, message.Records, "patient_id"));
        Assert.Equal("0651439A", AstmFieldAccessor.Read(paths, message.Records, "specimen_id"));
        Assert.Equal("Group", AstmFieldAccessor.Read(paths, message.Records, "test_code"));
        Assert.Equal("Mono Ctrl", AstmFieldAccessor.Read(paths, message.Records, "subtest_code"));
        Assert.Equal("0", AstmFieldAccessor.Read(paths, message.Records, "subtest_result"));
    }

    [Fact]
    public void OrderBuilder_IncludesDonorCommentForCrossmatch()
    {
        var raw = AstmOrderBuilder.Build(
            new Patient
            {
                MedicalRecordNumber = "1171984",
                LastName = "Patient",
                FirstName = "Test",
                DateOfBirth = new DateOnly(1959, 4, 22),
                Sex = Sex.Male
            },
            new Specimen { AccessionNumber = "0651439A" },
            [new OrderLine { TestCode = "XM", IsActive = true }],
            [new InstrumentTestMapping
            {
                InstrumentCode = "Crossmatch",
                ApplicationCode = "XM",
                Kind = InstrumentMappingKind.Assay,
                Direction = InterfaceTranslationDirection.Both
            }],
            donorUnitNumber: "R02460");

        Assert.Contains("^^^Crossmatch", raw);
        Assert.Contains("C|1|L|Donor^R02460", raw);
    }

    [Fact]
    public void Simulator_GeneratesParseableRandomResults()
    {
        var generated = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "GroupScreen", "0651439A", "1171984", "Patient", "Test", "19590422", "M", Seed: 7));
        var message = AstmParser.Parse(generated.RawMessage);
        Assert.Equal(InstrumentMessageKind.Result, message.Kind);
        Assert.Contains(message.Records, r => r.Type == 'O' && r.GetComponent(5, 4) == "Group");
        Assert.Contains(message.Records, r => r.Type == 'O' && r.GetComponent(5, 4) == "Screen");
        Assert.Contains(message.Records, r => r.Type == 'L');
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(11)]
    [InlineData(42)]
    public void Simulator_GroupReactionsMatchInterpretation(int seed)
    {
        var generated = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC0010", "MRN0002", "Newborn", "Baby", "20260101", "M", seed));
        var message = AstmParser.Parse(generated.RawMessage);
        var grade = (string analyte) =>
            message.Records.First(r => r.GetComponent(3, 4) == analyte).GetComponent(4, 1);
        var interp = message.Records.First(r => r.GetComponent(3, 4) == "Group").GetField(4);

        Assert.Equal("0", grade("Mono Ctrl"));
        Assert.DoesNotContain("\\u002B", generated.RawMessage, StringComparison.Ordinal);
        Assert.Contains('+', generated.RawMessage);

        var abo = interp.StartsWith("AB", StringComparison.Ordinal) ? "AB"
            : interp.StartsWith('O') ? "O"
            : interp[..1];
        var rhPos = interp.Contains("Pos", StringComparison.OrdinalIgnoreCase);
        AssertPolarity(grade("Anti-A"), expectedPositive: abo is "A" or "AB");
        AssertPolarity(grade("Anti-B"), expectedPositive: abo is "B" or "AB");
        AssertPolarity(grade("Anti-D1"), expectedPositive: rhPos);
        AssertPolarity(grade("Anti-D2"), expectedPositive: rhPos);
        AssertPolarity(grade("A1 Cells"), expectedPositive: abo is "B" or "O");
        AssertPolarity(grade("B Cells"), expectedPositive: abo is "A" or "O");
    }

    [Fact]
    public void Simulator_ScreenAndCrossmatchMatchInterpretation()
    {
        var screen = AstmParser.Parse(EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Screen", "ACC0010", "MRN0002", "Newborn", "Baby", "20260101", "M", 8)).RawMessage);
        var screenInterp = screen.Records.First(r => r.GetComponent(3, 4) == "Screen").GetField(4);
        var cells = screen.Records
            .Where(r => r.Type == 'R' && r.GetComponent(3, 4).StartsWith("Screen ", StringComparison.Ordinal))
            .Select(r => r.GetComponent(4, 1))
            .ToList();
        if (screenInterp == "Negative")
        {
            Assert.All(cells, g => Assert.Equal("0", g));
        }
        else
        {
            Assert.Contains(cells, g => g != "0");
        }

        var xm = AstmParser.Parse(EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Crossmatch", "ACC0001", "MRN0001", "Demo", "Patricia", "19800101", "F", 9)).RawMessage);
        var xmInterp = xm.Records.First(r => r.GetComponent(3, 4) == "Crossmatch").GetField(4);
        var igg = xm.Records.First(r => r.GetComponent(3, 4) == "IgG XM").GetComponent(4, 1);
        Assert.Equal("4+", xm.Records.First(r => r.GetComponent(3, 4) == "Ind Ctrl").GetComponent(4, 1));
        if (xmInterp.Contains("Comp", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal("0", igg);
        }
        else
        {
            Assert.NotEqual("0", igg);
        }
    }

    private static void AssertPolarity(string grade, bool expectedPositive)
    {
        if (expectedPositive)
        {
            Assert.NotEqual("0", grade);
            Assert.EndsWith("+", grade);
        }
        else
        {
            Assert.Equal("0", grade);
        }
    }
}
