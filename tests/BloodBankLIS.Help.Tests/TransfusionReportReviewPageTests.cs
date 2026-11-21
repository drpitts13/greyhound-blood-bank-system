using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>OCD-083 option B: a reported transfusion is reviewed in place on the supervisor queue (Cycle 700).</summary>
public class TransfusionReportReviewPageTests : BunitContext
{
    // TEST-BB-940: the reported transfusion shows as its own kind with Open unit and Review; Reviewed waits for a note,
    // then records the review and takes the row off the queue.
    [Fact]
    public void ReportedTransfusion_IsReviewedInPlace()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/supervisor-queue"] = new List<SupervisorQueueItemDto>
            {
                new(SupervisorQueueRule.KindTransfusionReported, "Transfused while Recalled — U-1", "The EHR reported U-1 transfused…",
                    "/inventory/units/11", "Review", PatientId: 5, ContextId: 7, Mrn: "MRN5", DueSoon: true)
            },
            ["/api/transfusions/7/report-review"] = new TransfusionReportToReviewDto(7, 11, "U-1", 5, "MRN5", UnitStatus.Recalled, "Donor reactive", null, DateTime.UtcNow)
        });
        var page = Render<SupervisorQueue>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-report-review='7']")));
        Assert.Contains("Reported transfusion", page.Markup);

        page.Find("[data-report-review='7']").Click();
        Assert.True(page.Find("[data-report-review-save]").HasAttribute("disabled"));
        page.Find("[data-report-review-note]").Input("Physician informed");
        page.Find("[data-report-review-save]").Click();

        page.WaitForAssertion(() => Assert.Contains("Reported transfusion reviewed.", page.Markup));
        Assert.DoesNotContain("Transfused while Recalled", page.Markup);
    }
}
