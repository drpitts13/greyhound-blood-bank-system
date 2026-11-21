using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// End-user testing pass 283: the chart header marked a type tested with a failed-QC reagent lot "Under QC review", with
/// which lot only in hover text; recalled units on In transit and Issued units, and a reaction remainder that cannot be
/// held, gave their reason the same way. They are written out.
/// </summary>
public class QcReviewHeaderDetailTests : BunitContext
{
    // TEST-BB-1309: the chart header names the lot whose QC failed under its Under QC review badge.
    [Fact]
    public void UnderQcReview_NamesTheLotInView()
    {
        var patient = new PatientDto(4, "MRN0004", "Kellerman", "Diane", null, new DateOnly(1957, 2, 18), Sex.Female, PatientStatus.Active, DateTime.UtcNow, "test");
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/reagents/patients/4/results-under-review"] = new List<PatientResultUnderQcReviewDto> { new(7, "ABORH", 3, "ANTI-D", "AD-24092") }
        });

        var header = Render<PatientChartHeader>(p => p.Add(c => c.Patient, patient));
        header.WaitForAssertion(() => Assert.NotNull(header.Find("[data-type-under-qc-review-detail]")));

        Assert.Contains("AD-24092", header.Find("[data-type-under-qc-review-detail]").TextContent);
    }
}
