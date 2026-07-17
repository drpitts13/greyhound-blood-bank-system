using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class OrderingLocationValidatorTests
{
    [Fact]
    public void Validate_RequiresCode()
    {
        var loc = new OrderingLocation { Code = "" };
        var result = OrderingLocationValidator.Validate(loc, duplicateCode: false);
        Assert.True(result.IsHardStopped);
    }
}
