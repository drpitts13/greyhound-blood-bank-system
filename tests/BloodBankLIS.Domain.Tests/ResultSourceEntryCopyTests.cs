using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ResultSourceEntryCopyTests
{
    [Fact]
    public void ShowsPanelHint_OnlyWhenCatalogLogicApplied()
    {
        Assert.True(ResultSourceEntryCopy.ShowsPanelHint(true));
        Assert.False(ResultSourceEntryCopy.ShowsPanelHint(false));
    }

    [Theory]
    [InlineData(ResultSource.Calculated, "Panel entered as Calculated (catalog interpretation).")]
    [InlineData(ResultSource.Manual, "Panel entered as Manual.")]
    [InlineData(ResultSource.Instrument, "Panel entered as Instrument.")]
    [InlineData(ResultSource.Interface, "Panel entered as Interface.")]
    public void PanelEntered_NamesStoredSource(ResultSource source, string expected)
    {
        Assert.Equal(expected, ResultSourceEntryCopy.PanelEntered(source));
    }

    [Theory]
    [InlineData(ResultSource.Calculated, "Result saved as Calculated (catalog interpretation).")]
    [InlineData(ResultSource.Manual, "Result saved as Manual.")]
    public void Saved_NamesStoredSource(ResultSource source, string expected)
    {
        Assert.Equal(expected, ResultSourceEntryCopy.Saved(source));
    }

    [Theory]
    [InlineData(ResultSource.Manual, "Result entered as Manual.")]
    [InlineData(ResultSource.Calculated, "Result entered as Calculated (catalog interpretation).")]
    [InlineData(ResultSource.Instrument, "Result entered as Instrument.")]
    [InlineData(ResultSource.Interface, "Result entered as Interface.")]
    public void Entered_NamesStoredSource(ResultSource source, string expected)
    {
        Assert.Equal(expected, ResultSourceEntryCopy.Entered(source));
    }

    [Fact]
    public void PanelHint_StatesCalculatedVersusTypedAbo()
    {
        Assert.Contains("Calculated", ResultSourceEntryCopy.PanelHint, StringComparison.Ordinal);
        Assert.Contains("Manual", ResultSourceEntryCopy.PanelHint, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedHint_StatesManualForNoPanel()
    {
        Assert.Contains("Manual", ResultSourceEntryCopy.TypedHint, StringComparison.Ordinal);
        Assert.Contains("no panel", ResultSourceEntryCopy.TypedHint, StringComparison.OrdinalIgnoreCase);
    }
}
