using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class InventorySearchScopeTests
{
    // TEST-BB-719: the default Inventory search lists units still on a shelf (expired ones included, to discard), not
    // discarded, modified, issued, or shipped ones; a typed unit number, a chosen status, or the box finds any unit.
    [Fact]
    public void DefaultSearch_ListsUnitsStillOnAShelf()
    {
        var scope = InventorySearchScope.Statuses(null, null, includeNotInStock: false)!;
        Assert.Equal(InventoryUnitScopes.OnShelf, scope);
        Assert.Contains(UnitStatus.Available, scope);
        Assert.Contains(UnitStatus.Expired, scope);
        Assert.DoesNotContain(UnitStatus.Discarded, scope);
        Assert.DoesNotContain(UnitStatus.Modified, scope);
        Assert.DoesNotContain(UnitStatus.Issued, scope);
        Assert.DoesNotContain(UnitStatus.ShippedOut, scope);

        Assert.Null(InventorySearchScope.Statuses("W0001230000002", null, includeNotInStock: false));
        Assert.Null(InventorySearchScope.Statuses(null, UnitStatus.Discarded, includeNotInStock: false));
        Assert.Null(InventorySearchScope.Statuses(null, null, includeNotInStock: true));
        Assert.NotNull(InventorySearchScope.Statuses("  ", null, includeNotInStock: false));
    }
}
