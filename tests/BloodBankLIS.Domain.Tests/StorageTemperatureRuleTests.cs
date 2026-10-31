using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-296: storage reading against the location range.
public class StorageTemperatureRuleTests
{
    [Theory]
    [InlineData(4.0, 1.0, 6.0, StorageTemperatureStatus.InRange)]
    [InlineData(1.0, 1.0, 6.0, StorageTemperatureStatus.InRange)]
    [InlineData(6.0, 1.0, 6.0, StorageTemperatureStatus.InRange)]
    [InlineData(0.9, 1.0, 6.0, StorageTemperatureStatus.BelowRange)]
    [InlineData(6.1, 1.0, 6.0, StorageTemperatureStatus.AboveRange)]
    [InlineData(-25.0, -30.0, -18.0, StorageTemperatureStatus.InRange)]
    [InlineData(-17.5, -30.0, -18.0, StorageTemperatureStatus.AboveRange)]
    public void Evaluate_ComparesWithBothBounds(double reading, double min, double max, StorageTemperatureStatus expected)
    {
        Assert.Equal(expected, StorageTemperatureRule.Evaluate((decimal)reading, (decimal)min, (decimal)max));
    }

    [Fact]
    public void Evaluate_OneSidedRange_ChecksOnlyThatSide()
    {
        Assert.Equal(StorageTemperatureStatus.InRange, StorageTemperatureRule.Evaluate(-40m, null, -18m));
        Assert.Equal(StorageTemperatureStatus.AboveRange, StorageTemperatureRule.Evaluate(-10m, null, -18m));
        Assert.Equal(StorageTemperatureStatus.InRange, StorageTemperatureRule.Evaluate(30m, 20m, null));
        Assert.Equal(StorageTemperatureStatus.BelowRange, StorageTemperatureRule.Evaluate(19.9m, 20m, null));
    }

    [Fact]
    public void Evaluate_NoRange_IsNeverAnExcursion()
    {
        var status = StorageTemperatureRule.Evaluate(50m, null, null);
        Assert.Equal(StorageTemperatureStatus.NoRange, status);
        Assert.False(StorageTemperatureRule.IsExcursion(status));
        Assert.True(StorageTemperatureRule.IsExcursion(StorageTemperatureStatus.BelowRange));
        Assert.True(StorageTemperatureRule.IsExcursion(StorageTemperatureStatus.AboveRange));
        Assert.False(StorageTemperatureRule.IsExcursion(StorageTemperatureStatus.InRange));
    }

    [Theory]
    [InlineData(-100.0, true)]
    [InlineData(60.0, true)]
    [InlineData(-100.1, false)]
    [InlineData(60.1, false)]
    public void IsPlausible_GuardsAgainstTypingErrors(double reading, bool expected)
    {
        Assert.Equal(expected, StorageTemperatureRule.IsPlausible((decimal)reading));
    }

    [Fact]
    public void IsInFuture_AllowsFiveMinutesOfClockDrift()
    {
        var now = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(StorageTemperatureRule.IsInFuture(now.AddMinutes(5), now));
        Assert.True(StorageTemperatureRule.IsInFuture(now.AddMinutes(6), now));
        Assert.False(StorageTemperatureRule.IsInFuture(now.AddHours(-3), now));
    }

    [Fact]
    public void StoredStatuses_ExcludeUnitsThatLeftTheLocation()
    {
        Assert.Contains(UnitStatus.Available, StorageTemperatureRule.StoredStatuses);
        Assert.Contains(UnitStatus.Quarantine, StorageTemperatureRule.StoredStatuses);
        Assert.DoesNotContain(UnitStatus.Issued, StorageTemperatureRule.StoredStatuses);
        Assert.DoesNotContain(UnitStatus.Transfused, StorageTemperatureRule.StoredStatuses);
        Assert.DoesNotContain(UnitStatus.Discarded, StorageTemperatureRule.StoredStatuses);
    }
}

// TEST-BB-304: OCD-036 sustained-excursion quarantine timing.
public class StorageExcursionQuarantineRuleTests
{
    private static readonly DateTime T0 = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OngoingStart_IsTheFirstReadingOfTheUnbrokenOutOfRangeRun()
    {
        var readings = new[]
        {
            (T0, StorageTemperatureStatus.AboveRange),
            (T0.AddMinutes(10), StorageTemperatureStatus.InRange),
            (T0.AddMinutes(20), StorageTemperatureStatus.AboveRange),
            (T0.AddMinutes(40), StorageTemperatureStatus.BelowRange)
        };
        Assert.Equal(T0.AddMinutes(20), StorageTemperatureRule.OngoingExcursionStart(readings));
        Assert.Null(StorageTemperatureRule.OngoingExcursionStart([(T0, StorageTemperatureStatus.AboveRange), (T0.AddMinutes(5), StorageTemperatureStatus.InRange)]));
        Assert.Null(StorageTemperatureRule.OngoingExcursionStart([]));
    }

    [Fact]
    public void Quarantine_OnlyAfterMoreThanTheLimit_AndZeroTurnsItOff()
    {
        Assert.False(StorageTemperatureRule.RequiresQuarantine(T0, T0.AddMinutes(30), 30));
        Assert.True(StorageTemperatureRule.RequiresQuarantine(T0, T0.AddMinutes(30).AddSeconds(1), 30));
        Assert.False(StorageTemperatureRule.RequiresQuarantine(null, T0.AddHours(5), 30));
        Assert.False(StorageTemperatureRule.RequiresQuarantine(T0, T0.AddHours(5), 0));
    }
}
