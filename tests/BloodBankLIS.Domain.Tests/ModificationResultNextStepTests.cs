using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ModificationResultNextStepTests
{
    [Fact]
    public void ReleaseCue_Quarantine_NamesThePlaceAndTheRelease()
    {
        var cue = ModificationResultNextStep.ReleaseCue(UnitStatus.Quarantine, "Fridge A");

        Assert.Equal("In quarantine at Fridge A. Release it before it can be issued.", cue);
    }

    [Fact]
    public void ReleaseCue_QuarantineWithoutALocation_DoesNotInventAPlace()
    {
        var cue = ModificationResultNextStep.ReleaseCue(UnitStatus.Quarantine, StorageLocationLabel.None);

        Assert.Equal("In quarantine. No storage location is recorded. Release it before it can be issued.", cue);
    }

    [Theory]
    [InlineData(UnitStatus.Assigned)]
    [InlineData(UnitStatus.Available)]
    [InlineData(UnitStatus.Received)]
    public void ReleaseCue_NotQuarantine_HasNoReleaseStep(UnitStatus status)
    {
        Assert.Null(ModificationResultNextStep.ReleaseCue(status, "Fridge A"));
        Assert.Null(ModificationResultNextStep.ReleaseHref(status, 15, canRelease: true));
    }

    [Fact]
    public void ReleaseHref_QuarantineWithPermission_OpensThatUnitsReleaseRow()
    {
        Assert.Equal(
            "/inventory?board=quarantine&unitId=15#quality-quarantine",
            ModificationResultNextStep.ReleaseHref(UnitStatus.Quarantine, 15, canRelease: true));
    }

    [Fact]
    public void ReleaseHref_WithoutPermission_DoesNotOfferRelease()
    {
        Assert.Null(ModificationResultNextStep.ReleaseHref(UnitStatus.Quarantine, 15, canRelease: false));
        Assert.Null(ModificationResultNextStep.ReleaseHref(UnitStatus.Quarantine, 0, canRelease: true));
    }

    [Fact]
    public void QuarantineReleaseTarget_OpensOnlyAUnitOnTheBoard()
    {
        Assert.Equal(15, ModificationResultNextStep.QuarantineReleaseTarget("quarantine", 15, [15, 16]));
        Assert.Null(ModificationResultNextStep.QuarantineReleaseTarget("quarantine", 99, [15, 16]));
        Assert.Null(ModificationResultNextStep.QuarantineReleaseTarget("retype", 15, [15]));
        Assert.Null(ModificationResultNextStep.QuarantineReleaseTarget(null, 15, [15]));
    }

    [Fact]
    public void AliquotParentCue_ConfirmedParent_NamesStatusAndRemainingVolume()
    {
        var cue = ModificationResultNextStep.AliquotParentCue(true, " W1234 ", UnitStatus.Available, 150.5m);

        Assert.Equal("W1234 stayed Available with 150.5 mL remaining.", cue);
    }

    [Fact]
    public void AliquotParentCue_UnrecordedVolume_DoesNotInventOne()
    {
        var cue = ModificationResultNextStep.AliquotParentCue(true, "W1234", UnitStatus.Available, null);

        Assert.Equal("W1234 stayed Available. Its volume is not recorded.", cue);
    }

    [Fact]
    public void AliquotParentCue_UnconfirmedParent_DoesNotInventStatusOrVolume()
    {
        var cue = ModificationResultNextStep.AliquotParentCue(false, "W1234", UnitStatus.Available, 150m);

        Assert.Equal("The parent was left in place. Its remaining status and volume could not be confirmed.", cue);
    }

    [Fact]
    public void AliquotParentCue_ReportsTheParentStatusItWasGiven()
    {
        var cue = ModificationResultNextStep.AliquotParentCue(true, "W1234", UnitStatus.Quarantine, 40m);

        Assert.Equal("W1234 stayed Quarantine with 40 mL remaining.", cue);
    }

    [Fact]
    public void RetiredSourceCue_ConfirmedSources_NamesEachStatus()
    {
        var cue = ModificationResultNextStep.RetiredSourceCue(
        [
            new ModificationResultNextStep.RetiredSourceFact("W1", true, UnitStatus.Modified),
            new ModificationResultNextStep.RetiredSourceFact(" W2 ", true, UnitStatus.Modified)
        ]);

        Assert.Equal("W1 is Modified. W2 is Modified.", cue);
    }

    [Fact]
    public void RetiredSourceCue_UnconfirmedSource_DoesNotInventModified()
    {
        var cue = ModificationResultNextStep.RetiredSourceCue(
        [
            new ModificationResultNextStep.RetiredSourceFact("W1", false, UnitStatus.Modified)
        ]);

        Assert.Equal("W1 was retired. Its current status could not be confirmed.", cue);
    }

    [Fact]
    public void RetiredSourceCue_ReportsTheStatusItWasGiven()
    {
        var cue = ModificationResultNextStep.RetiredSourceCue(
        [
            new ModificationResultNextStep.RetiredSourceFact("W1", true, UnitStatus.Available)
        ]);

        Assert.Equal("W1 is Available.", cue);
        Assert.Null(ModificationResultNextStep.RetiredSourceCue([]));
    }
}
