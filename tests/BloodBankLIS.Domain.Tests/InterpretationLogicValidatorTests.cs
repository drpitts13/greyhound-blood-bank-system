using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class InterpretationLogicValidatorTests
{
    [Fact]
    public void Validate_MatchingReactions_Allows()
    {
        var logic = InterpretationLogicDefinitions.DefaultAboRhLogic();
        var catalog = BuildCatalog();
        var subtests = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AboRhPanelSubtestCodes.AntiA] = "0",
            [AboRhPanelSubtestCodes.AntiB] = "0",
            [AboRhPanelSubtestCodes.AntiD] = "4+",
            [AboRhPanelSubtestCodes.ACells] = "4+",
            [AboRhPanelSubtestCodes.BCells] = "4+"
        };

        var result = InterpretationLogicValidator.Validate(
            logic,
            catalog,
            InterpretationLogicDefinitions.BuildAboRhKey(AboGroup.O, RhType.Positive),
            subtests);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void Validate_MismatchingReaction_HardStops()
    {
        var logic = InterpretationLogicDefinitions.DefaultAboRhLogic();
        var catalog = BuildCatalog();
        var subtests = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AboRhPanelSubtestCodes.AntiA] = "4+",
            [AboRhPanelSubtestCodes.AntiB] = "0",
            [AboRhPanelSubtestCodes.AntiD] = "4+",
            [AboRhPanelSubtestCodes.ACells] = "4+",
            [AboRhPanelSubtestCodes.BCells] = "4+"
        };

        var result = InterpretationLogicValidator.Validate(
            logic,
            catalog,
            InterpretationLogicDefinitions.BuildAboRhKey(AboGroup.O, RhType.Positive),
            subtests);

        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, h => h.Code == "INTERPRETATION.MISMATCH");
    }

    private static Dictionary<string, SubtestDefinition> BuildCatalog()
    {
        var choices = SubtestChoiceDefinitions.ToJson(SubtestChoiceDefinitions.DefaultGradedReaction());
        return AboRhPanelSubtestCodes.All.ToDictionary(
            code => code,
            code => new SubtestDefinition
            {
                Code = code,
                Name = code,
                ResultType = SubtestResultType.GradedReaction,
                ChoicesJson = choices,
                IsActive = true
            },
            StringComparer.OrdinalIgnoreCase);
    }
}
