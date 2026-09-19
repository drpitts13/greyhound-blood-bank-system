using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class SpecimenExpirationCalculatorTests
{
    private static readonly DateTime Collected = new(2026, 7, 4, 11, 56, 0, DateTimeKind.Utc);

    [Fact]
    public void Exact_ThreeDays_KeepsClockTime()
    {
        var expires = SpecimenExpirationCalculator.ComputeExpiresUtc(
            Collected, new SpecimenExpirationCode(3, SpecimenExpirationUnit.Days), SpecimenExpirationMode.ExactTime);

        Assert.Equal(new DateTime(2026, 7, 7, 11, 56, 0, DateTimeKind.Utc), expires);
    }

    [Fact]
    public void EndOfDay_ThreeDays_Uses2359()
    {
        var expires = SpecimenExpirationCalculator.ComputeExpiresUtc(
            Collected, new SpecimenExpirationCode(3, SpecimenExpirationUnit.Days), SpecimenExpirationMode.EndOfDay);

        Assert.Equal(new DateTime(2026, 7, 7, 23, 59, 0, DateTimeKind.Utc), expires);
    }

    [Fact]
    public void EndOfDay_TwoWeeks_Uses2359()
    {
        var expires = SpecimenExpirationCalculator.ComputeExpiresUtc(
            Collected, new SpecimenExpirationCode(2, SpecimenExpirationUnit.Weeks), SpecimenExpirationMode.EndOfDay);

        Assert.Equal(new DateTime(2026, 7, 18, 23, 59, 0, DateTimeKind.Utc), expires);
    }

    [Fact]
    public void Hours_IgnoresEndOfDay()
    {
        var expires = SpecimenExpirationCalculator.ComputeExpiresUtc(
            Collected, new SpecimenExpirationCode(72, SpecimenExpirationUnit.Hours), SpecimenExpirationMode.EndOfDay);

        Assert.Equal(new DateTime(2026, 7, 7, 11, 56, 0, DateTimeKind.Utc), expires);
    }

    [Fact]
    public void Exact_SevenDays_Matches168Hours()
    {
        var days = SpecimenExpirationCalculator.ComputeExpiresUtc(
            Collected, new SpecimenExpirationCode(7, SpecimenExpirationUnit.Days), SpecimenExpirationMode.ExactTime);
        var hours = SpecimenExpirationCalculator.ComputeExpiresUtc(
            Collected, new SpecimenExpirationCode(168, SpecimenExpirationUnit.Hours), SpecimenExpirationMode.ExactTime);

        Assert.Equal(days, hours);
        Assert.Equal(Collected.AddHours(168), days);
    }

    [Fact]
    public void CalendarMonth_January31_ClampsToFebruary()
    {
        var collected = new DateTime(2026, 1, 31, 10, 0, 0, DateTimeKind.Utc);
        var expires = SpecimenExpirationCalculator.ComputeExpiresUtc(
            collected, new SpecimenExpirationCode(1, SpecimenExpirationUnit.Months), SpecimenExpirationMode.ExactTime);

        Assert.Equal(new DateTime(2026, 2, 28, 10, 0, 0, DateTimeKind.Utc), expires);
    }

    [Fact]
    public void CalendarMonth_LeapYear_UsesFebruary29()
    {
        var collected = new DateTime(2028, 1, 31, 10, 0, 0, DateTimeKind.Utc);
        var expires = SpecimenExpirationCalculator.ComputeExpiresUtc(
            collected, new SpecimenExpirationCode(1, SpecimenExpirationUnit.Months), SpecimenExpirationMode.EndOfDay);

        Assert.Equal(new DateTime(2028, 2, 29, 23, 59, 0, DateTimeKind.Utc), expires);
    }

    [Fact]
    public void ParseOverload_AcceptsCanonicalCode()
    {
        var expires = SpecimenExpirationCalculator.ComputeExpiresUtc(Collected, "3D", SpecimenExpirationMode.ExactTime);

        Assert.Equal(new DateTime(2026, 7, 7, 11, 56, 0, DateTimeKind.Utc), expires);
    }
}
