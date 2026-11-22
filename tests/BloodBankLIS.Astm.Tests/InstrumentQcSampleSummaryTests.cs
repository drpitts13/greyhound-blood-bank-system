using BloodBankLIS.Astm.Messaging;

namespace BloodBankLIS.Astm.Tests;

public class InstrumentQcSampleSummaryTests
{
    // TEST-BB-1126: a QC sample's result records read as "test: reaction" in order; a message that does not parse, or has
    // no result record, gives none (end-user testing pass 109).
    [Fact]
    public void Results_ReadEachResultRecord()
    {
        const string raw = "H|\\^&|||ImmuLINK|||||LIS|||LIS2-A2|20261114195044\r" +
                           "O|1|QC-ABSC-POS||^^^Screen|R\r" +
                           "R|1|^^^Screen 1|0^0^0|||||F\r" +
                           "R|2|^^^Pos Ctrl|4+^4+^90|||||F\r" +
                           "R|3|^^^Screen|Positive|||||F\r" +
                           "R|4|^^^Screen 2||||||F\r" +
                           "L|1|N\r";
        Assert.Equal(["Screen 1: 0", "Pos Ctrl: 4+", "Screen: Positive", "Screen 2: (no value)"], InstrumentQcSampleSummary.Results(raw));
        Assert.Empty(InstrumentQcSampleSummary.Results("H|\\^&\rL|1|N\r"));
        Assert.Empty(InstrumentQcSampleSummary.Results(null));
        Assert.Empty(InstrumentQcSampleSummary.Results("not astm"));
    }
}
