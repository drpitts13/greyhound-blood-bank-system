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

    [Fact]
    public void ForInventoryList_NamedLocation_UsesTheNameNotTheCode()
    {
        var catalog = new[]
        {
            new StorageLocationLabel.CatalogLocation(12, "  Paperwork fridge  ", "FR-HOLD")
        };

        Assert.Equal("Paperwork fridge", StorageLocationLabel.ForInventoryList(12, catalog));
    }

    [Fact]
    public void ForInventoryList_BlankName_DoesNotFallBackToTheCode()
    {
        var catalog = new[]
        {
            new StorageLocationLabel.CatalogLocation(12, "   ", "FR-HOLD")
        };

        Assert.Equal("Location 12", StorageLocationLabel.ForInventoryList(12, catalog));
        Assert.Equal("Location 18", StorageLocationLabel.ForInventoryList(18, catalog));
        Assert.Equal("Location 4", StorageLocationLabel.ForInventoryList(4, catalog: null));
    }

    [Fact]
    public void ForInventoryList_NoLocation_DoesNotInventOne()
    {
        var catalog = new[]
        {
            new StorageLocationLabel.CatalogLocation(12, "Paperwork fridge", "FR-HOLD")
        };

        Assert.Equal(StorageLocationLabel.None, StorageLocationLabel.ForInventoryList(null, catalog));
    }

    [Fact]
    public void ForUnitChart_NamedLocation_UsesTheNameNotTheCode()
    {
        var catalog = new[]
        {
            new StorageLocationLabel.CatalogLocation(12, "  Outdate fridge  ", "FR-OUT")
        };

        Assert.Equal("Outdate fridge", StorageLocationLabel.ForUnitChart(12, catalog));
    }

    [Fact]
    public void ForUnitChart_BlankName_DoesNotFallBackToTheCode()
    {
        var catalog = new[]
        {
            new StorageLocationLabel.CatalogLocation(12, "   ", "FR-OUT")
        };

        Assert.Equal("Location 12", StorageLocationLabel.ForUnitChart(12, catalog));
        Assert.Equal("Location 18", StorageLocationLabel.ForUnitChart(18, catalog));
        Assert.Equal("Location 4", StorageLocationLabel.ForUnitChart(4, catalog: null));
    }

    [Fact]
    public void ForUnitChart_NoLocation_DoesNotInventOne()
    {
        var catalog = new[]
        {
            new StorageLocationLabel.CatalogLocation(12, "Outdate fridge", "FR-OUT")
        };

        Assert.Equal(StorageLocationLabel.None, StorageLocationLabel.ForUnitChart(null, catalog));
    }

    [Fact]
    public void ForPicker_KeepsTheCodeAndTheLocationIdWhenTheNameIsBlank()
    {
        Assert.Equal("FR1 — Issue fridge", StorageLocationLabel.ForPicker(9, " FR1 ", " Issue fridge "));
        Assert.Equal("FR1 — Location 9", StorageLocationLabel.ForPicker(9, " FR1 ", "   "));
        Assert.DoesNotContain("FR1 — FR1", StorageLocationLabel.ForPicker(9, "FR1", "   "));
        Assert.Equal("Location 9", StorageLocationLabel.ForPicker(9, "  ", null));
    }
}
