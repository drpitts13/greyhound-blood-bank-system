using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class PanelSubtestDefinitionTests
{
    [Fact]
    public void ParseAndSerialize_RoundTrips()
    {
        var items = PanelSubtestDefinitions.DefaultAboRh();
        var json = PanelSubtestDefinitions.ToJson(items);
        var parsed = PanelSubtestDefinitions.Parse(json);
        Assert.Equal(items.Count, parsed.Count);
        Assert.Equal("Anti-A", parsed[0].Code);
    }

    [Fact]
    public void AboRhPanelValidator_UsesConfiguredRequiredSubtests()
    {
        IReadOnlyList<PanelSubtestDefinition> configured =
        [
            new PanelSubtestDefinition("X1", "Test 1", true),
            new PanelSubtestDefinition("X2", "Test 2", false)
        ];
        var panel = new AboRhPanelResult(AboGroup.A, RhType.Positive, new Dictionary<string, string>
        {
            ["X1"] = "0"
        });

        var result = AboRhPanelValidator.Validate(panel, configured);
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void AssignmentParse_AcceptsLegacyAndNewFormat()
    {
        var legacy = PanelSubtestDefinitions.ToJson(PanelSubtestDefinitions.DefaultAboRh());
        var legacyParsed = PanelSubtestAssignments.Parse(legacy);
        Assert.Equal(7, legacyParsed.Count);
        Assert.Equal("Anti-A", legacyParsed[0].SubtestCode);

        var json = PanelSubtestAssignments.ToJson([
            new PanelSubtestAssignment("Anti-A", true, 1)
        ]);
        var parsed = PanelSubtestAssignments.Parse(json);
        Assert.Single(parsed);
        Assert.Equal("Anti-A", parsed[0].SubtestCode);
    }

    [Fact]
    public void TestDefinitionValidator_RequiresPanelSubtestsForAboRh()
    {
        var def = new Domain.Entities.Configuration.TestDefinition
        {
            Code = "PANEL",
            Name = "Panel",
            ResultValueType = ResultValueType.AboRh
        };

        var result = TestDefinitionValidator.Validate(def, duplicateActiveCode: false);
        Assert.True(result.IsHardStopped);
    }

    [Fact]
    public void TestDefinitionValidator_WithAssignmentsAndCatalog_Passes()
    {
        var def = new Domain.Entities.Configuration.TestDefinition
        {
            Code = "PANEL",
            Name = "Panel",
            ResultValueType = ResultValueType.AboRh,
            PanelSubtestsJson = PanelSubtestAssignments.ToJson([
                new PanelSubtestAssignment("Anti-A", true, 1)
            ])
        };

        var catalog = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Anti-A" };
        var result = TestDefinitionValidator.Validate(def, duplicateActiveCode: false, catalog);
        Assert.False(result.IsHardStopped);
    }
}
