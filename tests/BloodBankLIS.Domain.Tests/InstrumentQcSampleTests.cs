using BloodBankLIS.Domain.Instruments;

namespace BloodBankLIS.Domain.Tests;

/// <summary>End-user testing pass 84: an instrument's own QC samples, recognised by sample ID prefix.</summary>
public class InstrumentQcSampleTests
{
    // TEST-BB-1097: prefixes are trimmed, upper-cased and de-duplicated; a sample ID starting with one (any case) is QC;
    // nothing is QC when no prefixes are set or the ID is blank.
    [Fact]
    public void IsQc_MatchesTheInstrumentsPrefixes()
    {
        Assert.Equal("QC-, CTRL", InstrumentQcSample.Normalize(" qc-; ctrl QC- "));
        Assert.Null(InstrumentQcSample.Normalize(" , "));
        Assert.True(InstrumentQcSample.IsQc("qc-absc-pos", "QC-, CTRL"));
        Assert.True(InstrumentQcSample.IsQc(" CTRL0042", "QC-, CTRL"));
        Assert.False(InstrumentQcSample.IsQc("ACC-0042", "QC-, CTRL"));
        Assert.False(InstrumentQcSample.IsQc("QC-ABSC-POS", null));
        Assert.False(InstrumentQcSample.IsQc(" ", "QC-"));
    }
}
