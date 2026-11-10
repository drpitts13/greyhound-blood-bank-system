using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>OCD-073 (decided 2026-11-10): outside antibody reports on the chart are documented and confirmed here.</summary>
public class OutsideAntibodyChartTests : BunitContext
{
    private static readonly DateTime Now = DateTime.UtcNow;

    // TEST-BB-864: an unconfirmed outside report shows how it was documented, "Not yet confirmed here" and Confirm; a
    // confirmed one names who confirmed it and the testing; confirming asks for that testing before it can be saved.
    // Naming the reporting facility asks how the report is documented.
    [Fact]
    public void OutsideReports_ShowDocumentationAndConfirmation()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        AntibodyDto Ab(long id, DateTime? confirmed) => new(
            id, 1, null, "Anti-Jka", AntibodyStatus.Identified, true, null, null, Now.AddDays(-2), "tech1",
            ReportingFacility: "St. Mary's Hospital", ReportKind: OutsideReportKind.Phone, ReportDocumentation: "Dr. Lee, 555-0199",
            ConfirmedUtc: confirmed, ConfirmedBy: confirmed is null ? null : "tech2", ConfirmationNote: confirmed is null ? null : "Panel 2026-11-10");
        var tab = Render<PatientChartOverviewTab>(p => p
            .Add(x => x.PatientId, 1)
            .Add(x => x.AntibodyHistory, [Ab(5, null), Ab(6, Now)]));

        Assert.Contains("Phone report from Dr. Lee, 555-0199, taken by tech1", tab.FindAll("[data-antibody-documented]")[0].TextContent);
        Assert.Single(tab.FindAll("[data-antibody-unconfirmed]"));
        Assert.Contains("by tech2: Panel 2026-11-10", tab.Find("[data-antibody-confirmed]").TextContent);
        Assert.Single(tab.FindAll("[data-antibody-confirm]"));

        tab.Find("[data-antibody-confirm='5']").Click();
        Assert.True(tab.Find("[data-antibody-confirm-save]").HasAttribute("disabled"));
        tab.Find("[data-antibody-confirm-note]").Input("Panel 2026-11-10, workup #12");
        Assert.False(tab.Find("[data-antibody-confirm-save]").HasAttribute("disabled"));

        Assert.Empty(tab.FindAll("[data-antibody-report]"));
        tab.Render(p => p.Add(x => x.AbFacility, "County General"));
        Assert.NotNull(tab.Find("[data-antibody-report-kind]"));
    }
}
