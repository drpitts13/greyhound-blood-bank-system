using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Tests;

public class BloodSupplierChoiceTests
{
    [Fact]
    public void DisplayName_KeepsTheCodeAndTheSupplierIdWhenTheNameIsBlank()
    {
        var named = Supplier(9, " ARC ", " American Red Cross ");
        Assert.Equal("ARC — American Red Cross", BloodSupplierRefDto.From(named).DisplayName);
        Assert.Equal("ARC — American Red Cross", BloodSupplierDto.From(named).DisplayName);
        Assert.Equal(" American Red Cross ", BloodSupplierRefDto.From(named).Name);

        var blank = Supplier(9, "ARC", "   ");
        Assert.Equal("ARC — Supplier 9", BloodSupplierRefDto.From(blank).DisplayName);
        Assert.Equal("ARC — Supplier 9", BloodSupplierDto.From(blank).DisplayName);
        Assert.Equal("   ", BloodSupplierDto.From(blank).Name);
        Assert.DoesNotContain("ARC — ARC", BloodSupplierRefDto.From(blank).DisplayName);
    }

    private static BloodSupplier Supplier(long id, string code, string name) => new()
    {
        Id = id,
        Code = code,
        Name = name,
        Registration = "FDA-1"
    };
}
