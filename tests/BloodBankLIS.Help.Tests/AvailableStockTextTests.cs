using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class AvailableStockTextTests
{
    private static BloodUnitDto Unit(long id, long? reservedFor) =>
        new BloodUnitDto(id, $"W00000000000{id}", null, null, null, null, null, null, null, null, null, false, 1,
            AboGroup.O, RhType.Positive, "O+", DateTime.UtcNow.AddDays(10), UnitStatus.Available, null,
            ComponentEntrySource.Manual, DateTime.UtcNow, "tech1") { ReservedForPatientId = reservedFor };

    // TEST-BB-1324 (end-user testing pass 297): the home screen's Available units tile says how many of them are reserved
    // for patients; none reserved says nothing.
    [Fact]
    public void ReservedNote_CountsUnitsReservedForPatients()
    {
        Assert.Equal("2 reserved for patients", AvailableStockText.ReservedNote([Unit(1, 10), Unit(2, 12), Unit(3, null)]));
        Assert.Null(AvailableStockText.ReservedNote([Unit(3, null)]));
    }
}
