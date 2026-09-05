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
            "Retype saved as O+. A second user must verify before the unit is released.",
            ProductRetypeEntryCopy.Saved(AboGroup.O, RhType.Positive));
    }

    [Fact]
    public void VerifiedAvailable_NamesInterpretedType()
    {
        Assert.Equal(
            "Retype verified as O+. Unit is Available.",
            ProductRetypeEntryCopy.VerifiedAvailable(AboGroup.O, RhType.Positive));
    }

    [Fact]
    public void VerifiedQuarantine_IncludesDiscrepancyWhenPresent()
    {
        Assert.Equal(
            "Retype verified as A+. Anti-A does not match labeled O.",
            ProductRetypeEntryCopy.VerifiedQuarantine(AboGroup.A, RhType.Positive, "Anti-A does not match labeled O."));
        Assert.Equal(
            "Retype verified as A+. Unit moved to Quarantine.",
            ProductRetypeEntryCopy.VerifiedQuarantine(AboGroup.A, RhType.Positive, null));
    }
}
