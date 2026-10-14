using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class StorageLocationLabelTests
{
    [Fact]
    public void Format_NamedLocation_UsesTheName()
    {
        var names = new Dictionary<long, string?> { [12] = "  Fridge A  " };

        Assert.Equal("Fridge A", StorageLocationLabel.Format(12, names));
        Assert.Equal("Fridge A", StorageLocationLabel.Format(12, "  Fridge A  "));
    }

    [Fact]
    public void Format_BlankOrUnknownName_KeepsTheLocationId()
    {
        var names = new Dictionary<long, string?> { [12] = "   " };

        Assert.Equal("Location 12", StorageLocationLabel.Format(12, names));
        Assert.Equal("Location 18", StorageLocationLabel.Format(18, names));
        Assert.Equal("Location 18", StorageLocationLabel.Format(18, "  "));
        Assert.Equal("Location 4", StorageLocationLabel.Format(4, namesById: null));
    }

    [Fact]
    public void Format_NoLocation_DoesNotInventOne()
    {
        Assert.Equal(StorageLocationLabel.None, StorageLocationLabel.Format(null, namesById: null));
        Assert.Equal("No location", StorageLocationLabel.Format(null, "   "));
    }
}
