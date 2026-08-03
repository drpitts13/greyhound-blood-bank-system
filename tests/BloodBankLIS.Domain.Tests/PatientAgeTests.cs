using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class PatientAgeTests
{
    private static readonly DateTime Now = new(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BornTodayIsZeroDaysOld()
    {
        var age = PatientAge.FromDateOfBirth(new DateOnly(2026, 5, 30), Now);

        Assert.Equal(0, age.Days);
        Assert.Equal(0, age.Months);
        Assert.Equal(0, age.Years);
    }

    [Fact]
    public void BornYesterdayIsOneDayOld()
    {
        Assert.Equal(1, PatientAge.FromDateOfBirth(new DateOnly(2026, 5, 29), Now).Days);
    }

    [Fact]
    public void WholeMonthsAndYearsAreFloored()
    {
        var age = PatientAge.FromDateOfBirth(new DateOnly(2024, 6, 1), Now);

        Assert.Equal(23, age.Months);
        Assert.Equal(1, age.Years);
    }

    [Fact]
    public void BirthdayNotYetReachedThisMonth()
    {
        var age = PatientAge.FromDateOfBirth(new DateOnly(2000, 5, 31), Now);

        Assert.Equal(25, age.Years);
        Assert.Equal(311, age.Months);
    }

    [Fact]
    public void BirthdayReachedThisMonth()
    {
        Assert.Equal(26, PatientAge.FromDateOfBirth(new DateOnly(2000, 5, 30), Now).Years);
    }

    [Fact]
    public void FutureDateOfBirthYieldsZeroRatherThanNegative()
    {
        var age = PatientAge.FromDateOfBirth(new DateOnly(2027, 1, 1), Now);

        Assert.Equal(PatientAge.Zero, age);
    }
}
