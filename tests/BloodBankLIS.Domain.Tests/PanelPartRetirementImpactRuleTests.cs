using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class PanelPartRetirementImpactRuleTests
{
    private static readonly PanelPartRetirementImpactRule.PanelTest[] Tests =
    [
        new("ABSC", [new PanelSubtestAssignment("Cell1", true, 0, ["IS", "37C", "AHG"]), new PanelSubtestAssignment("Cell2", true, 1, ["IS", "AHG"])]),
        new("XM", [new PanelSubtestAssignment("IS", true), new PanelSubtestAssignment("AHG", true)]),
        new("ABORH", [new PanelSubtestAssignment("Anti-A", true)])
    ];

    // TEST-BB-1404 (end-user testing loop 2 tick 24): retiring a subtest or phase names the active tests whose panels use
    // it (a phase by the phases assigned to any subtest); one nothing uses gives no warning.
    [Fact]
    public void Warnings_NameThePanelsUsingIt()
    {
        Assert.Contains("subtest Cell1 is still in the panel of 1 test (ABSC)",
            PanelPartRetirementImpactRule.SubtestWarnings("Cell1", Tests).Single().Message);
        Assert.Single(PanelPartRetirementImpactRule.SubtestWarnings("cell1", Tests));
        Assert.Contains("phase 37C is still in the panel of 1 test (ABSC)", PanelPartRetirementImpactRule.PhaseWarnings("37C", Tests).Single().Message);
        Assert.Contains("subtest AHG is still in the panel of 1 test (XM)", PanelPartRetirementImpactRule.SubtestWarnings("AHG", Tests).Single().Message);
        Assert.Empty(PanelPartRetirementImpactRule.SubtestWarnings("PEG", Tests));
        Assert.Empty(PanelPartRetirementImpactRule.PhaseWarnings("ENZ", Tests));
    }
}
