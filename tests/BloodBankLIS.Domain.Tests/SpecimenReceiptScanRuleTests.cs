using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-350
public class SpecimenReceiptScanRuleTests
{
    [Theory]
    [InlineData("ACC1", "ACC1", null, true)]
    [InlineData("ACC1", "acc1", null, true)]
    [InlineData("LBL9", "ACC1", "lbl9", true)]
    [InlineData("ACC2", "ACC1", "LBL9", false)]
    [InlineData("", "ACC1", null, false)]
    public void Matches_TheAccessionOrTheLabelBarcode(string scan, string accession, string? barcode, bool expected) =>
        Assert.Equal(expected, SpecimenReceiptScanRule.Matches(scan, accession, barcode));

    [Fact]
    public void Normalize_TrimsAndUppercases() =>
        Assert.Equal("ACC-7", SpecimenReceiptScanRule.Normalize("  acc-7 "));

    [Fact]
    public void NotFound_NamesTheScan_OrAsksForOne()
    {
        Assert.Contains("ACC-9", SpecimenReceiptScanRule.NotFound("ACC-9").Message);
        Assert.Contains("Scan the tube", SpecimenReceiptScanRule.NotFound("").Message);
    }
}
