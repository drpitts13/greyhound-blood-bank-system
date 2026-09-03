using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests.Isbt128;

public class ComponentScanVerifierTests
{
    private static BloodUnit SampleUnit() => new()
    {
        Din = "G123417654321",
        ProductCodeData = "E0206000",
        AboRhdCode = "DEMO",
        ExpirationEncoded = "2250200",
        Status = UnitStatus.Issued,
        ComponentIdentity = "G123417654321|E0206000"
    };

    [Fact]
    public void MatchingScan_Passes()
    {
        var unit = SampleUnit();
        var eval = ComponentScanVerifier.Verify(unit, "G123417654321", "E0206000", null, "DEMO", "2250200");
        Assert.False(eval.IsHardStopped);
    }

    [Fact]
    public void DinMismatch_HardStop()
    {
        var unit = SampleUnit();
        var eval = ComponentScanVerifier.Verify(unit, "G123417654399", "E0206000", null, "DEMO", "2250200");
        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == IsbtErrorCodes.UnitScanMismatch);
    }

    [Fact]
    public void Recalled_HardStop()
    {
        var unit = SampleUnit();
        unit.Status = UnitStatus.Recalled;
        var eval = ComponentScanVerifier.Verify(unit, "G123417654321", "E0206000", null, "DEMO", "2250200");
        Assert.Contains(eval.HardStops, r => r.Code == IsbtErrorCodes.ComponentRecalled);
    }

    [Fact]
    public void OnHold_HardStop()
    {
        var unit = SampleUnit();
        unit.Status = UnitStatus.OnHold;
        var eval = ComponentScanVerifier.Verify(unit, "G123417654321", "E0206000", null, "DEMO", "2250200");
        Assert.Contains(eval.HardStops, r => r.Code == IsbtErrorCodes.ComponentOnHold);
    }

    [Fact]
    public void Missing_HardStop()
    {
        var unit = SampleUnit();
        unit.Status = UnitStatus.Missing;
        var eval = ComponentScanVerifier.Verify(unit, "G123417654321", "E0206000", null, "DEMO", "2250200");
        Assert.Contains(eval.HardStops, r => r.Code == IsbtErrorCodes.ComponentMissing);
    }

    [Fact]
    public void Damaged_HardStop()
    {
        var unit = SampleUnit();
        unit.Status = UnitStatus.Damaged;
        var eval = ComponentScanVerifier.Verify(unit, "G123417654321", "E0206000", null, "DEMO", "2250200");
        Assert.Contains(eval.HardStops, r => r.Code == IsbtErrorCodes.ComponentDamaged);
    }
}
