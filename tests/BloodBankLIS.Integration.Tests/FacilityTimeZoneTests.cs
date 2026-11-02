using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Infrastructure.Common;
using Microsoft.Extensions.Configuration;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-328: bedside documents print the facility's local time; a missing or unknown zone falls back visibly.
public class FacilityTimeZoneTests
{
    [Fact]
    public void ConfiguredZone_ConvertsAcrossDaylightSaving()
    {
        IFacilityTimeZone tz = FacilityTimeZone.FromId("America/Chicago");

        Assert.Equal(new DateTime(2026, 1, 15, 12, 0, 0), tz.ToLocal(new DateTime(2026, 1, 15, 18, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 7, 15, 13, 0, 0), tz.ToLocal(new DateTime(2026, 7, 15, 18, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(tz.Zone.DisplayName, tz.Label);
    }

    [Fact]
    public void UnspecifiedKind_IsTreatedAsUtc()
    {
        IFacilityTimeZone tz = FacilityTimeZone.FromId("America/Chicago");
        Assert.Equal(new DateTime(2026, 1, 15, 12, 0, 0), tz.ToLocal(new DateTime(2026, 1, 15, 18, 0, 0)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Blank_UsesTheServerZone_AndSaysSo(string? id)
    {
        var tz = FacilityTimeZone.FromId(id);
        Assert.Equal(TimeZoneInfo.Local.Id, tz.Zone.Id);
        Assert.Contains("server time zone", tz.Label);
    }

    [Fact]
    public void UnknownZone_FallsBackToTheServerZone_AndNamesTheTypo()
    {
        var tz = FacilityTimeZone.FromId("Mars/Olympus_Mons");
        Assert.Equal(TimeZoneInfo.Local.Id, tz.Zone.Id);
        Assert.Contains("\"Mars/Olympus_Mons\" was not found", tz.Label);
    }

    [Fact]
    public void ReadsTheConfigurationKey()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [FacilityTimeZone.ConfigurationKey] = "America/Chicago" })
            .Build();
        Assert.Equal(FacilityTimeZone.FromId("America/Chicago").Zone.Id, FacilityTimeZone.FromConfiguration(config).Zone.Id);
    }
}
