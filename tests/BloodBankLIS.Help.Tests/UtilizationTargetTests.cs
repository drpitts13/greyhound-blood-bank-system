using BloodBankLIS.Application.Reporting;
using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>Blood utilization marks a line above the facility's C:T ratio target (end-user testing pass 342).</summary>
public class UtilizationTargetTests : BunitContext
{
    private static UtilizationRowDto Row(int crossmatched, int transfused, decimal? ratio) =>
        new("ICU", "(no provider)", crossmatched, transfused, transfused, ratio, null, null);

    // TEST-BB-1381: a ratio above the target is marked; one at or below it, or any line with no target set, is not.
    [Fact]
    public void Row_MarksAboveTarget()
    {
        Assert.NotNull(Render<UtilizationRow>(p => p.Add(r => r.Row, Row(6, 2, 3.00m)).Add(r => r.Target, 2.0m))
            .Find("[data-ct-above-target]"));
        Assert.Empty(Render<UtilizationRow>(p => p.Add(r => r.Row, Row(4, 2, 2.00m)).Add(r => r.Target, 2.0m))
            .FindAll("[data-ct-above-target]"));
        Assert.Empty(Render<UtilizationRow>(p => p.Add(r => r.Row, Row(6, 2, 3.00m)))
            .FindAll("[data-ct-above-target]"));
    }
}
