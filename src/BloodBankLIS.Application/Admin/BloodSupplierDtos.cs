using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed record BloodSupplierDto(
    long Id,
    string Code,
    string Name,
    string? Address,
    string Registration,
    bool IsActive,
    string DisplayName)
{
    public static BloodSupplierDto From(BloodSupplier supplier) => new(
        supplier.Id,
        supplier.Code,
        supplier.Name,
        supplier.Address,
        supplier.Registration,
        supplier.IsActive,
        BloodSupplierLabel.ForChoice(supplier.Id, supplier.Code, supplier.Name));
}

public sealed record SaveBloodSupplierRequest(
    string Code,
    string Name,
    string? Address,
    string Registration);
