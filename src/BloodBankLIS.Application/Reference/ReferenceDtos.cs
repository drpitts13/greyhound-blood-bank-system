using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Reference;

public sealed record ProductTypeDto(
    long Id,
    string ProductCode,
    string Name,
    ComponentClass ComponentClass,
    int? DefaultShelfLifeHours,
    bool RequiresCrossmatch,
    bool IsActive,
    string? Isbt128ProductCode = null,
    bool RequiresRetype = false)
{
    public string DisplayProductCode =>
        string.IsNullOrWhiteSpace(Isbt128ProductCode) ? ProductCode : Isbt128ProductCode;

    public static ProductTypeDto From(ProductType t) => new(
        t.Id, t.ProductCode, t.Name, t.ComponentClass, t.DefaultShelfLifeHours, t.RequiresCrossmatch, t.IsActive,
        t.Isbt128ProductCode, t.RequiresRetype);
}

public sealed record InventoryLocationDto(
    long Id,
    string Code,
    string Name,
    LocationType LocationType,
    bool IsActive)
{
    public static InventoryLocationDto From(InventoryLocation l) => new(
        l.Id, l.Code, l.Name, l.LocationType, l.IsActive);
}

public sealed record OrderingLocationRefDto(long Id, string Code, string? Name, string? Department, bool IsActive, string DisplayName)
{
    public static OrderingLocationRefDto From(OrderingLocation l) => new(
        l.Id, l.Code, l.Name, l.Department, l.IsActive,
        string.IsNullOrWhiteSpace(l.Name) ? l.Code : l.Name);
}

public sealed record OrderingProviderRefDto(long Id, string ProviderId, string Name, string? Specialty, string? Location)
{
    public static OrderingProviderRefDto From(OrderingProvider p) => new(
        p.Id, p.ProviderId, p.Name, p.Specialty, p.Location);
}

public sealed record TestDefinitionListItemDto(string Code, string Name, TestCategory Category)
{
    public static TestDefinitionListItemDto From(TestDefinition t) => new(t.Code, t.Name, t.Category);
}

public sealed record DirectoryUserDto(string UserName, string DisplayName)
{
    public string Label =>
        string.IsNullOrWhiteSpace(DisplayName) || string.Equals(DisplayName, UserName, StringComparison.Ordinal)
            ? UserName
            : $"{DisplayName} ({UserName})";
}
