using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class OrderingProviderValidatorTests
{
    [Fact]
    public void Validate_RejectsMissingIdAndName()
    {
        var p = new OrderingProvider { ProviderId = "", Name = "" };
        var result = OrderingProviderValidator.Validate(p, duplicateProviderId: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == "PROVIDER.ID.REQUIRED");
        Assert.Contains(result.HardStops, r => r.Code == "PROVIDER.NAME.REQUIRED");
    }

    [Fact]
    public void Validate_RejectsDuplicateId()
    {
        var p = new OrderingProvider { ProviderId = "P1", Name = "Dr. One" };
        var result = OrderingProviderValidator.Validate(p, duplicateProviderId: true);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == "PROVIDER.ID.DUPLICATE");
    }
}
