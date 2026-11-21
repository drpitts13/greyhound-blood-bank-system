using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

public class PregnancySectionTests : BunitContext
{
    // TEST-BB-1261: the chart's Recent pregnancy section is not offered on an infant's chart (under one year), unless a date
    // is already on file; an adult's chart offers it (end-user testing pass 237).
    [Fact]
    public void RecentPregnancy_IsNotOfferedOnAnInfantsChart()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var today = DateOnly.FromDateTime(DateTime.Today);

        var infant = Render<PatientChartOverviewTab>(p => p.Add(x => x.PatientId, 2).Add(x => x.PatientDateOfBirth, today.AddDays(-12)));
        Assert.Empty(infant.FindAll("[data-pregnancy-current]"));

        var documented = Render<PatientChartOverviewTab>(p => p.Add(x => x.PatientId, 2).Add(x => x.PatientDateOfBirth, today.AddDays(-12))
            .Add(x => x.RecentPregnancyUtc, DateTime.UtcNow.AddDays(-1)));
        Assert.Single(documented.FindAll("[data-pregnancy-current]"));

        var adult = Render<PatientChartOverviewTab>(p => p.Add(x => x.PatientId, 3).Add(x => x.PatientDateOfBirth, new DateOnly(1990, 1, 1)));
        Assert.Single(adult.FindAll("[data-pregnancy-current]"));
    }
}
