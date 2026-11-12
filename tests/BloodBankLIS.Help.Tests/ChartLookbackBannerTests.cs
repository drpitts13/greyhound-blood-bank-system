using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>The chart says when the patient received a unit from a recalled donation (Cycle 699).</summary>
public class ChartLookbackBannerTests : BunitContext
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly PatientDto Patient = new(5, "MRN0005", "Recipient", "Rae", null, new DateOnly(1970, 1, 1), Sex.Female, PatientStatus.Active, Now, "test");

    private static LookbackNotificationDto Note(long id, long? patientId, string unit, LookbackNotificationStatus status) => new(
        id, "W000058585858", id + 100, patientId, id + 200, status, null, null, null, null, unit);

    // TEST-BB-935: an open notification for this patient shows on the chart with the unit, the donation and its status,
    // linking to Lookback; another patient's notification does not.
    [Fact]
    public void OpenNotification_ShowsOnTheChart()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/lookback/notifications"] = new List<LookbackNotificationDto>
            {
                Note(1, 5, "U-RCL-1", LookbackNotificationStatus.Attempted),
                Note(2, 6, "U-OTHER", LookbackNotificationStatus.Pending)
            }
        });
        var header = Render<PatientChartHeader>(p => p.Add(c => c.Patient, Patient));
        header.WaitForAssertion(() => Assert.NotNull(header.Find("[data-chart-lookback]")));

        var banner = header.Find("[data-chart-lookback]").TextContent;
        Assert.Contains("received a unit from a recalled donation", banner);
        Assert.Contains("U-RCL-1", banner);
        Assert.Contains("W000058585858", banner);
        Assert.Contains("Attempted", banner);
        Assert.DoesNotContain("U-OTHER", banner);
        Assert.Equal("/lookback", header.Find("[data-chart-lookback-link]").GetAttribute("href"));
    }

    // TEST-BB-936: no open notification for this patient, no banner.
    [Fact]
    public void NoNotification_NoBanner()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/lookback/notifications"] = new List<LookbackNotificationDto> { Note(2, 6, "U-OTHER", LookbackNotificationStatus.Pending) }
        });
        var header = Render<PatientChartHeader>(p => p.Add(c => c.Patient, Patient));
        header.WaitForAssertion(() => Assert.Contains("MRN0005", header.Markup));
        Assert.Empty(header.FindAll("[data-chart-lookback]"));
    }

    // TEST-BB-1020: after a merge succeeds the merge panel closes and its confirmation shows on the chart (end-user
    // testing pass 12: the panel closed and took the message with it).
    [Fact]
    public void MergeOutcome_ShowsAfterThePanelCloses()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var header = Render<PatientChartHeader>(p => p
            .Add(c => c.Patient, Patient)
            .Add(c => c.ShowMerge, false)
            .Add(c => c.MergeFeedback, BloodBankLIS.Web.Services.Feedback.Succeed("Duplicate merged into this patient.")));
        header.WaitForAssertion(() => Assert.Contains("Duplicate merged into this patient.", header.Find("[data-merge-outcome]").TextContent));
    }
}
