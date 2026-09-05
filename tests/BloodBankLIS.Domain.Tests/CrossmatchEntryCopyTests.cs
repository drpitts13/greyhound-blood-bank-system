using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class CrossmatchEntryCopyTests
{
    [Fact]
    public void Recorded_NamesMethodAndCompatible()
    {
        Assert.Equal(
            "Serologic crossmatch recorded as Compatible (#12).",
            CrossmatchEntryCopy.Recorded(CrossmatchMethod.Serologic, CrossmatchResult.Compatible, 12));
    }

    [Fact]
    public void Recorded_NamesElectronicAndIncompatible()
    {
        Assert.Equal(
            "Electronic crossmatch recorded as Incompatible (#4).",
            CrossmatchEntryCopy.Recorded(CrossmatchMethod.Electronic, CrossmatchResult.Incompatible, 4));
    }

    [Fact]
    public void Recorded_NamesAhgMethod()
    {
        Assert.Equal(
            "Ahg crossmatch recorded as Compatible (#1).",
            CrossmatchEntryCopy.Recorded(CrossmatchMethod.Ahg, CrossmatchResult.Compatible, 1));
    }

    [Fact]
    public void Recorded_WithoutId_NamesMethodAndResult()
    {
        Assert.Equal(
            "Serologic crossmatch recorded as Compatible.",
            CrossmatchEntryCopy.Recorded(CrossmatchMethod.Serologic, CrossmatchResult.Compatible));
    }

    [Fact]
    public void WorklistSaved_NamesSourceMethodAndResult()
    {
        Assert.Equal(
            "Result saved as Manual. Serologic crossmatch recorded as Compatible. Verified.",
            CrossmatchEntryCopy.WorklistSaved(
                ResultSource.Manual, CrossmatchMethod.Serologic, CrossmatchResult.Compatible, verified: true));
        Assert.Equal(
            "Result saved as Manual. Ahg crossmatch recorded as Incompatible. Incomplete.",
            CrossmatchEntryCopy.WorklistSaved(
                ResultSource.Manual, CrossmatchMethod.Ahg, CrossmatchResult.Incompatible, verified: false));
    }
}
