using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class UnitQuarantineReasonTextTests
{
    // TEST-BB-1213: the unit page names the quarantine reason in words ("Pending release"), not its code
    // ("PendingRelease"; end-user testing pass 190).
    [Fact]
    public void QuarantineReason_IsShownInWords()
    {
        Assert.Equal("Pending release", Ui.EnumText(UnitQuarantineReason.PendingRelease));

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var page = File.ReadAllText(Path.Combine(dir!.FullName, "src", "BloodBankLIS.Web", "Components", "Pages", "UnitDetail.razor"));
        Assert.Contains("Quarantine: @Ui.EnumText(_unit.QuarantineReasonCode)", page);
    }
}
