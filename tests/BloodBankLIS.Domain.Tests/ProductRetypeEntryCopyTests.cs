using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ProductRetypeEntryCopyTests
{
    [Fact]
    public void Format_NamesAboAndRhSign()
    {
        Assert.Equal("O+", ProductRetypeEntryCopy.Format(AboGroup.O, RhType.Positive));
        Assert.Equal("A\u2212", ProductRetypeEntryCopy.Format(AboGroup.A, RhType.Negative));
    }

    [Fact]
    public void Format_StatesWhenAntiDWasNotPerformed()
    {
        Assert.Equal("O (Anti-D not performed)", ProductRetypeEntryCopy.Format(AboGroup.O, null));
    }

    [Fact]
    public void Saved_NamesInterpretedType()
    {
        Assert.Equal(
            "Retype recorded as O+. Unit is Available.",
            ProductRetypeEntryCopy.Saved(AboGroup.O, RhType.Positive));
    }

    [Fact]
    public void SavedQuarantine_IncludesDiscrepancyWhenPresent()
    {
        Assert.Equal(
            "Retype recorded as A+. Anti-A does not match labeled O.",
            ProductRetypeEntryCopy.SavedQuarantine(AboGroup.A, RhType.Positive, "Anti-A does not match labeled O."));
        Assert.Equal(
            "Retype recorded as A+. Unit moved to Quarantine.",
            ProductRetypeEntryCopy.SavedQuarantine(AboGroup.A, RhType.Positive, null));
    }
}
