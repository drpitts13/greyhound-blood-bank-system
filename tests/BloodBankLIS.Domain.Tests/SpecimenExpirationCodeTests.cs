using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class SpecimenExpirationCodeTests
{
    [Theory]
    [InlineData("72H", 72, SpecimenExpirationUnit.Hours)]
    [InlineData("72h", 72, SpecimenExpirationUnit.Hours)]
    [InlineData("3D", 3, SpecimenExpirationUnit.Days)]
    [InlineData("3d", 3, SpecimenExpirationUnit.Days)]
    [InlineData("2W", 2, SpecimenExpirationUnit.Weeks)]
    [InlineData("2w", 2, SpecimenExpirationUnit.Weeks)]
    [InlineData("3M", 3, SpecimenExpirationUnit.Months)]
    [InlineData("3m", 3, SpecimenExpirationUnit.Months)]
    [InlineData(" 12 H ", 12, SpecimenExpirationUnit.Hours)]
    [InlineData("1D", 1, SpecimenExpirationUnit.Days)]
    public void TryParse_ValidCodes_Succeeds(string code, int expectedAmount, SpecimenExpirationUnit expectedUnit)
    {
        var ok = SpecimenExpirationCode.TryParse(code, out var result);

        Assert.True(ok);
        Assert.Equal(expectedAmount, result.Amount);
        Assert.Equal(expectedUnit, result.Unit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("24")]
    [InlineData("H")]
    [InlineData("24X")]
    [InlineData("0H")]
    [InlineData("-5D")]
    [InlineData("5.5D")]
    [InlineData("3Y")]
    public void TryParse_InvalidCodes_Fails(string? code)
    {
        var ok = SpecimenExpirationCode.TryParse(code, out var result);

        Assert.False(ok);
        Assert.Equal(default, result);
    }

    [Theory]
    [InlineData(72, SpecimenExpirationUnit.Hours, "72H")]
    [InlineData(3, SpecimenExpirationUnit.Days, "3D")]
    [InlineData(2, SpecimenExpirationUnit.Weeks, "2W")]
    [InlineData(3, SpecimenExpirationUnit.Months, "3M")]
    public void ToString_FormatsCanonically(int amount, SpecimenExpirationUnit unit, string expected)
    {
        var code = new SpecimenExpirationCode(amount, unit);

        Assert.Equal(expected, code.ToString());
    }
}
