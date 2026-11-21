using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class OrderActionAttachableTests
{
    // TEST-BB-1385 (end-user testing loop 2 tick 3): an order may ask for a deglycerolized or plasma-reduced unit; a
    // freeze or rejuvenation is inventory work, not a patient order action, and divide/pool/aliquot are not attachable.
    [Theory]
    [InlineData(ModificationType.Deglycerolize, true)]
    [InlineData(ModificationType.PlasmaReduction, true)]
    [InlineData(ModificationType.Irradiate, true)]
    [InlineData(ModificationType.Freeze, false)]
    [InlineData(ModificationType.Rejuvenate, false)]
    [InlineData(ModificationType.Pool, false)]
    public void IsOrderAttachable_CoversPatientOrderedModifications(ModificationType type, bool expected) =>
        Assert.Equal(expected, OrderActionValidator.IsOrderAttachable(type));
}
