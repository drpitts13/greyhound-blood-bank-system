using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class StorageLocationStatusTests
{
    // TEST-BB-1147: a unit that has left the bank says so with where it was last stored; a unit in the bank shows its
    // location as before (end-user testing pass 126).
    [Theory]
    [InlineData(UnitStatus.Issued, "Not in storage (issued) · last stored at Main Fridge")]
    [InlineData(UnitStatus.TransfusionStarted, "Not in storage (transfusing) · last stored at Main Fridge")]
    [InlineData(UnitStatus.ShippedOut, "Not in storage (shipped out) · last stored at Main Fridge")]
    [InlineData(UnitStatus.Discarded, "Not in storage (discarded) · last stored at Main Fridge")]
    [InlineData(UnitStatus.Available, "Main Fridge")]
    [InlineData(UnitStatus.Quarantine, "Main Fridge")]
    [InlineData(UnitStatus.Crossmatched, "Main Fridge")]
    [InlineData(UnitStatus.Expired, "Main Fridge")]
    public void WithStatus(UnitStatus status, string expected) =>
        Assert.Equal(expected, StorageLocationLabel.WithStatus(status, "Main Fridge"));
}
