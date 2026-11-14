using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

/// <summary>The thaw prompt states the shelf life that will apply, and refuses before Yes (Cycle 685, OCD-068).</summary>
public class AutoThawPreviewTests
{
    private static PatientAllocationRowDto Row(int? open, int? pooled, bool isPool) => new(
        1, 2, "W000000000001", "E0707", "Plasma", false, ProductAllocationDisplayStatus.ReadyForIssue, AllocationStatus.Reserved,
        null, null, null, null, null, DateTime.UtcNow, "tech", null,
        AutoThawProductCode: "E0707", AutoThawShelfLifeHours: 24,
        AutoThawOpenSystemShelfLifeHours: open, AutoThawPooledShelfLifeHours: pooled, AutoThawPooled: isPool);

    // TEST-BB-902: a closed single unit uses the closed time whatever else is set; ticking open system uses the
    // open-system time, or names the refusal when it is not set; a pool needs the pooled time even with the box clear,
    // and takes the shorter when both apply.
    [Fact]
    public void Preview_FollowsTheBox_AndThePool()
    {
        Assert.Equal(((int?)24, (string?)null), AutoThawPreview.For(Row(null, null, isPool: false), openSystem: false));
        Assert.Equal(((int?)4, (string?)null), AutoThawPreview.For(Row(4, null, isPool: false), openSystem: true));

        var openNotSet = AutoThawPreview.For(Row(null, 6, isPool: false), openSystem: true);
        Assert.Null(openNotSet.Hours);
        Assert.Contains("No open-system thawed shelf life is set for E0707", openNotSet.Refusal);

        var poolNotSet = AutoThawPreview.For(Row(4, null, isPool: true), openSystem: false);
        Assert.Null(poolNotSet.Hours);
        Assert.Contains("No pooled thawed shelf life is set for E0707", poolNotSet.Refusal);

        Assert.Equal(((int?)6, (string?)null), AutoThawPreview.For(Row(4, 6, isPool: true), openSystem: false));
        Assert.Equal(((int?)4, (string?)null), AutoThawPreview.For(Row(4, 6, isPool: true), openSystem: true));
    }
}
