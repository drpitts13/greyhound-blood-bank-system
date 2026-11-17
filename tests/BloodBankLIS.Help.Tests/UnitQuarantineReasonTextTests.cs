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

public class EnumWordsOnScreenTests
{
    // TEST-BB-1214: status and kind columns show words, not joined codes ("Pending verification", "Not applicable";
    // end-user testing pass 191), on the pages that rendered the raw value.
    [Fact]
    public void StatusAndKindColumns_ShowWords()
    {
        Assert.Equal("Pending verification", Ui.EnumText(ResultStatus.PendingVerification));
        Assert.Equal("Not applicable", Ui.EnumText(FatalityNotificationStatus.NotApplicable));
        Assert.Equal("already words", Ui.EnumText("already words"));

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var components = Path.Combine(dir!.FullName, "src", "BloodBankLIS.Web", "Components");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine([components, .. parts]));
        Assert.Contains("@Ui.EnumText(r.FatalityNotificationStatus)", Read("Pages", "Reactions.razor"));
        Assert.Contains("@Ui.EnumText(item.CurrentResultStatus)", Read("Shared", "PatientChartTestsTab.razor"));
        Assert.Contains("@Ui.EnumText(h.EventType)", Read("Shared", "PatientChartProductHistoryTab.razor"));
        Assert.Contains("@Ui.EnumText(row.EventType)", Read("Pages", "UnitDetail.razor"));
        Assert.Contains("@Ui.EnumText(e.ErrorType)", Read("Pages", "Hl7.razor"));
    }
}

public class CrossmatchCueLinkTests
{
    // TEST-BB-1215: a reserved unit that cannot be crossmatched from the chart says where to: its order (to link a
    // specimen), or Compatibility prefilled with the unit and specimen when the reservation has no order (pass 193).
    [Fact]
    public void ReservedUnitCrossmatchCue_LinksWhereToGo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var tab = File.ReadAllText(Path.Combine(dir!.FullName, "src", "BloodBankLIS.Web", "Components", "Shared", "PatientChartProductsTab.razor"));
        Assert.Contains("data-xm-on-compatibility", tab);
        Assert.Contains("$\"/compatibility?patientId={Patient?.Id ?? 0}&bloodUnitId={a.BloodUnitId}\"", tab);
        Assert.Contains("\"&from=xm\"", tab);
        Assert.Contains("PatientChartUrls.Order(Patient?.Id ?? 0, a.OrderNumber)", tab);
        Assert.DoesNotContain("<span class=\"muted\">Link a specimen to the order before crossmatch.</span>", tab);
    }
}
