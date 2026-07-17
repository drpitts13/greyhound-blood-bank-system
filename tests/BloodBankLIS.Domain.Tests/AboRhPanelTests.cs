using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class AboRhPanelTests
{
    [Fact]
    public void FormatPanel_RoundTrips_WithAllRequiredSubtests()
    {
        var subtests = new Dictionary<string, string>
        {
            [AboRhPanelSubtestCodes.AntiA] = "0",
            [AboRhPanelSubtestCodes.AntiB] = "4+",
            [AboRhPanelSubtestCodes.AntiD] = "0",
            [AboRhPanelSubtestCodes.ACells] = "0",
            [AboRhPanelSubtestCodes.BCells] = "4+",
            [AboRhPanelSubtestCodes.Control] = "0"
        };

        var panel = new AboRhPanelResult(AboGroup.A, RhType.Positive, subtests);
        var stored = AboRhResultValue.FormatPanel(panel);

        Assert.True(AboRhResultValue.TryParsePanel(stored, out var parsed));
        Assert.Equal(AboGroup.A, parsed.Abo);
        Assert.Equal(RhType.Positive, parsed.Rh);
        Assert.Equal("4+", parsed.GetSubtest(AboRhPanelSubtestCodes.AntiB));
    }

    [Fact]
    public void TryParse_LegacyPipeFormat_StillWorks()
    {
        Assert.True(AboRhResultValue.TryParse("O|Positive", out var aboRh));
        Assert.Equal(AboGroup.O, aboRh.Abo);
    }

    [Fact]
    public void Validate_MissingRequiredSubtest_HardStops()
    {
        var panel = new AboRhPanelResult(AboGroup.A, RhType.Positive, new Dictionary<string, string>
        {
            [AboRhPanelSubtestCodes.AntiA] = "0"
        });

        var eval = AboRhPanelValidator.Validate(panel);
        Assert.True(eval.IsHardStopped);
    }

    [Fact]
    public void FormatDisplay_Panel_ShowsSubtests()
    {
        var panel = new AboRhPanelResult(AboGroup.B, RhType.Negative, new Dictionary<string, string>
        {
            [AboRhPanelSubtestCodes.AntiA] = "0",
            [AboRhPanelSubtestCodes.AntiB] = "4+",
            [AboRhPanelSubtestCodes.AntiD] = "0",
            [AboRhPanelSubtestCodes.ACells] = "4+",
            [AboRhPanelSubtestCodes.BCells] = "0"
        });

        var display = AboRhResultValue.FormatDisplay(AboRhResultValue.FormatPanel(panel));
        Assert.Contains("Anti-A:0", display);
        Assert.Contains("Anti-B:4+", display);
    }
}
