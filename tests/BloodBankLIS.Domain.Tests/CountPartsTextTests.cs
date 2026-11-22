using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class CountPartsTextTests
{
    // TEST-BB-1318 (end-user testing pass 291): a cooler check or stock count summary leaves out the counts that are zero,
    // and a summary recorded with every count reads without them.
    [Fact]
    public void Summary_LeavesOutZeroCounts()
    {
        Assert.Equal("3 of 4 packed, 1 missing",
            CountPartsText.With("3 of 4 packed", (1, "missing"), (0, "issued to another cooler"), (0, "not issued"), (0, "unresolved scans")));
        Assert.Equal("2 of 2 found", CountPartsText.With("2 of 2 found", (0, "missing")));
        Assert.Equal("1 missing, 2 not issued", CountPartsText.NonZero((1, "missing"), (0, "x"), (2, "not issued")));

        Assert.Equal("3 of 4 packed, 1 missing",
            CountPartsText.DropZeroParts("3 of 4 packed, 1 missing, 0 issued to another cooler, 0 not issued, 0 unresolved scans"));
        Assert.Equal("0 of 4 packed, 4 missing", CountPartsText.DropZeroParts("0 of 4 packed, 4 missing, 0 not issued"));
        Assert.Equal("all 2 units packed", CountPartsText.DropZeroParts("all 2 units packed"));
    }
}
