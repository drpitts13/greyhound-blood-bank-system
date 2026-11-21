using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class QcReviewTextTests
{
    // TEST-BB-1204: the chart's blood type says when the ABO/Rh was tested with a lot whose QC failed and is not yet
    // reviewed; other tests under review do not mark the type (end-user testing pass 181).
    [Fact]
    public void AboRh_NamesTheLotUnderReview()
    {
        Assert.Null(QcReviewText.AboRh([]));
        Assert.Null(QcReviewText.AboRh([new PatientResultUnderQcReviewDto(2, "ABSC", 7, "AHG", "AHG-1")]));
        Assert.Equal(
            "ABO/Rh tested with ANTI-D lot AD-24092, whose QC failed; the result is to review on Reagent QC before relying on it.",
            QcReviewText.AboRh([new PatientResultUnderQcReviewDto(5, "ABORH", 3, "ANTI-D", "AD-24092")]));
    }
}

public class AntibodyCardQcReviewTests
{
    // TEST-BB-1220: the antibody card page warns before printing when the ABO/Rh it carries is under QC review
    // (end-user testing pass 198); like the chart, it does not stop printing.
    [Fact]
    public void AntibodyCard_WarnsWhenTheTypeIsUnderQcReview()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var page = File.ReadAllText(Path.Combine(dir!.FullName, "src", "BloodBankLIS.Web", "Components", "Pages", "AntibodyCard.razor"));
        Assert.Contains("QcReviewText.AboRh(_underQcReview)", page);
        Assert.Contains("GetPatientResultsUnderQcReviewAsync(PatientId)", page);
        Assert.Contains("data-antibody-card-qc-review", page);
    }
}

public class TestHistoryQcReviewTests
{
    // TEST-BB-1222: the chart's Test history marks a result still to review after a reagent lot it was tested with
    // failed QC, naming the lot (end-user testing pass 200).
    [Fact]
    public void TestHistory_MarksAResultUnderQcReview()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var tab = File.ReadAllText(Path.Combine(dir!.FullName, "src", "BloodBankLIS.Web", "Components", "Shared", "PatientChartTestHistoryTab.razor"));
        Assert.Contains("GetPatientResultsUnderQcReviewAsync(PatientId)", tab);
        Assert.Contains("_underQcReview.FirstOrDefault(u => u.ResultId == h.ResultId)", tab);
        Assert.Contains("data-history-qc-review", tab);
    }
}

public class ExmChecklistFoldTests
{
    // TEST-BB-1228: the chart's electronic crossmatch checklist is folded when the facility has electronic crossmatch off
    // and open when it is on (end-user testing pass 206).
    [Fact]
    public void ExmChecklist_IsFoldedWhileTheFacilityHasItOff()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var tab = File.ReadAllText(Path.Combine(dir!.FullName, "src", "BloodBankLIS.Web", "Components", "Shared", "PatientChartOverviewTab.razor"));
        Assert.Contains("<details open=\"@Exm.FacilityAllowsElectronicCrossmatch\" data-exm-checklist>", tab);
        Assert.Contains("Show the checklist ({Exm.Criteria.Count} items)", tab);
    }
}

public class InfantNoMotherNoteTests
{
    // TEST-BB-1229: an infant's chart with no mother linked says her type and antibodies are not shown, to anyone who
    // reads the chart, under the same age window as Link mother (end-user testing pass 207).
    [Fact]
    public void InfantChart_SaysNoMotherIsLinked()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var header = File.ReadAllText(Path.Combine(dir!.FullName, "src", "BloodBankLIS.Web", "Components", "Shared", "PatientChartHeader.razor"));
        var note = header.IndexOf("data-family-no-mother", StringComparison.Ordinal);
        Assert.True(note > 0);
        var condition = header.LastIndexOf("@if (family.Mother is null", note, StringComparison.Ordinal);
        Assert.DoesNotContain("PermissionCodes.PatientWrite", header[condition..note]);
        Assert.Contains("MotherInfantLinkRule.OfferLink", header[condition..note]);
        Assert.True(BloodBankLIS.Domain.Rules.MotherInfantLinkRule.OfferLink(new DateOnly(2026, 11, 6), new DateOnly(2026, 11, 17)));
    }
}
