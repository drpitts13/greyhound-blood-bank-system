using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-356
public class BloodTypeHistoryLabelTests
{
    [Theory]
    [InlineData(BloodTypeSource.TestResult, null, null, "Tested result")]
    [InlineData(BloodTypeSource.HistoricalImport, null, null, "Imported from a previous blood bank system")]
    [InlineData(BloodTypeSource.ManualEntry, ManualTypeDocumentation.NotDocumented, null, "Manual, not documented")]
    [InlineData(BloodTypeSource.ManualEntry, null, null, "Manual, not documented")]
    [InlineData(BloodTypeSource.ManualEntry, ManualTypeDocumentation.OutsideSource, "St Mary's report 44821", "Manual, from a documented outside source: St Mary's report 44821")]
    public void Source_IsPlainWords(BloodTypeSource source, ManualTypeDocumentation? doc, string? reference, string expected) =>
        Assert.Equal(expected, BloodTypeHistoryLabel.Source(source, doc, reference));

    [Fact]
    public void RetiredOrUndocumented_DoNotCount()
    {
        Assert.False(BloodTypeHistoryLabel.CountsAsDetermination(BloodTypeSource.TestResult, null, retired: true));
        Assert.False(BloodTypeHistoryLabel.CountsAsDetermination(BloodTypeSource.ManualEntry, ManualTypeDocumentation.NotDocumented, retired: false));
        Assert.True(BloodTypeHistoryLabel.CountsAsDetermination(BloodTypeSource.ManualEntry, ManualTypeDocumentation.PreviousBloodBankSystem, retired: false));
        Assert.True(BloodTypeHistoryLabel.CountsAsDetermination(BloodTypeSource.TestResult, null, retired: false));
    }
}
