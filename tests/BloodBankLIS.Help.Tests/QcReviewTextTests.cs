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
