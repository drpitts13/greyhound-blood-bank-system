using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class StorageReadingTextTests
{
    // TEST-BB-1245: a location's last reading says how old it is, and a location holding units with no reading ever
    // recorded says so (end-user testing pass 223); an empty location with no reading says nothing.
    [Fact]
    public void LastReading_SaysItsAgeAndFlagsStorageNeverRead()
    {
        var now = new DateTime(2026, 11, 17, 14, 0, 0, DateTimeKind.Utc);
        Assert.Equal("5 d ago", StorageReadingText.Age(now.AddDays(-5), now));
        Assert.Null(StorageReadingText.Age(null, now));

        Assert.Equal("No reading on record; 6 units stored here", StorageReadingText.NoReadingWithUnits(null, 6));
        Assert.Equal("No reading on record; 1 unit stored here", StorageReadingText.NoReadingWithUnits(null, 1));
        Assert.Null(StorageReadingText.NoReadingWithUnits(null, 0));
        Assert.Null(StorageReadingText.NoReadingWithUnits(now, 6));
    }

    // TEST-BB-1350 (pass 323): with the reading interval not set, the page says readings are not marked due and names
    // the setting; with it set, or with no location that has a range, it says nothing.
    [Fact]
    public void IntervalNotSet_IsSaid()
    {
        BloodBankLIS.Application.Inventory.StorageLocationTemperatureDto Loc(decimal? min, bool notSet) =>
            new(1, "FRIDGE-1", "Main", min, 6m, 4m, null, null, 35) { ReadingIntervalNotSet = notSet };
        Assert.Contains("reading interval is not set", StorageReadingText.IntervalNotSet([Loc(1m, true)]));
        Assert.Null(StorageReadingText.IntervalNotSet([Loc(1m, false)]));
        Assert.Null(StorageReadingText.IntervalNotSet([]));
    }
}
