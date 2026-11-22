using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

public class RhigInfantChartTests : BunitContext
{
    // TEST-BB-1262: on an infant's chart (under one year) the RhIG tab says a pregnancy event belongs on the mother's chart
    // and a new evaluation offers only Other exposure; an adult's chart offers all three (end-user testing pass 238).
    [Fact]
    public void InfantChart_OffersOnlyOtherExposure()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/patients/2/rhig"] = new List<BloodBankLIS.Application.Immunohematology.RhigEvaluationDto>(),
            ["/api/patients/3/rhig"] = new List<BloodBankLIS.Application.Immunohematology.RhigEvaluationDto>()
        });
        var today = DateOnly.FromDateTime(DateTime.Today);

        var infant = Render<PatientChartRhigTab>(p => p.Add(x => x.PatientId, 2).Add(x => x.PatientDateOfBirth, today.AddDays(-12)));
        Assert.Single(infant.FindAll("[data-rhig-infant-note]"));
        infant.Find("[data-rhig-new]").Click();
        Assert.Equal(["Other exposure"], infant.Find("[data-rhig-form] select").QuerySelectorAll("option").Select(o => o.TextContent.Trim()));

        var adult = Render<PatientChartRhigTab>(p => p.Add(x => x.PatientId, 3).Add(x => x.PatientDateOfBirth, new DateOnly(1990, 1, 1)));
        Assert.Empty(adult.FindAll("[data-rhig-infant-note]"));
        adult.Find("[data-rhig-new]").Click();
        Assert.Equal(3, adult.Find("[data-rhig-form] select").QuerySelectorAll("option").Length);
    }
}
