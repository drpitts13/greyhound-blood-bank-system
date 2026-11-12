using BloodBankLIS.Application.Results;

namespace BloodBankLIS.Application.Tests;

public class ResultValueTextTests
{
    // TEST-BB-995: a stored ABO/Rh panel reads as its type and reactions, and a subtest panel as its readings, not the
    // stored JSON (end-user testing, 2026-11-12: the bench worklist showed {"abo":"O",...}); plain values are unchanged.
    [Fact]
    public void For_ReadsPanelsAsStaffDo()
    {
        var aboRh = ResultValueText.For(
            "{\"abo\":\"O\",\"rh\":\"Positive\",\"version\":1,\"subtests\":{\"Anti-A\":\"0\",\"Anti-B\":\"0\",\"Anti-D\":\"3+\",\"A-Cells\":\"4+\",\"B-Cells\":\"4+\"}}");
        Assert.DoesNotContain("{", aboRh);
        Assert.StartsWith("O", aboRh);
        Assert.Contains("Anti-D:3+", aboRh);

        var screen = ResultValueText.For("{\"version\":1,\"subtests\":{\"SC1\":\"0\",\"SC2\":\"1+\"}}");
        Assert.DoesNotContain("{", screen);
        Assert.Contains("SC2:1+", screen);

        Assert.Equal("Negative", ResultValueText.For("Negative"));
        Assert.Equal("—", ResultValueText.For(null));
    }

    // TEST-BB-1033: a plain stored ABO/Rh value reads as its type, as a panel does ("O+"; end-user testing pass 26: "O|Positive").
    [Fact]
    public void For_ReadsPlainAboRhAsItsType()
    {
        Assert.Equal("O+", ResultValueText.For("O|Positive"));
        Assert.Equal("A-", ResultValueText.For("A|Negative"));
        Assert.Equal("Weak|Thing", ResultValueText.For("Weak|Thing"));
    }

    // TEST-BB-1040: a phased screen reads as cell and phase, not the stored key (end-user testing pass 29: "Cell1|AHG:1+").
    [Fact]
    public void For_ReadsPhasedCellsAsCellAndPhase()
    {
        var screen = ResultValueText.For("{\"version\":2,\"subtests\":{\"Cell1\":{\"AHG\":\"1+\"},\"Pos Ctrl\":{\"AHG\":\"4+\"}}}");
        Assert.Equal("Cell1 AHG:1+ · Pos Ctrl AHG:4+", screen);
    }
}
