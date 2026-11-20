using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Reference;

public sealed record ReceivePolicyDto(
    bool RequireSecondVerifier,
    bool RequireTemperature = true,
    bool RequireVisualInspection = true)
{
    /// <summary>The receipt temperature range (OCD-005, a facility setting).</summary>
    public int TemperatureMinC { get; init; } = 1;

    public int TemperatureMaxC { get; init; } = 10;
}

public sealed record FacilityPolicyValueDto(string Key, bool Enabled)
{
    /// <summary>The value of a whole-number policy (for example a choice 0 to 3); 0 for a yes/no policy.</summary>
    public int Value { get; init; }
}

/// <summary>
/// The blood bank's name and telephone, from configuration (Facility:Name, Facility:Phone), for documents read outside
/// it such as the patient's antibody card (end-user testing pass 80). Null when not configured.
/// </summary>
public sealed record FacilityIdentityDto(string? Name, string? Phone);

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

    /// <summary>
    /// Choice label. The display code stays so staff can select the product.
    /// A blank name stays the product id and is not replaced by the code.
    /// </summary>
    public string ChoiceLabel => ProductTypeLabel.ForChoice(Id, DisplayProductCode, Name);

    public static ProductTypeDto From(ProductType t) => new(
        t.Id, t.ProductCode, t.Name, t.ComponentClass, t.DefaultShelfLifeHours, t.RequiresCrossmatch, t.IsActive,
        t.Isbt128ProductCode, t.RequiresRetype);
}

public sealed record InventoryLocationDto(
    long Id,
    string Code,
    string Name,
    LocationType LocationType,
    bool IsActive,
    bool AllowsIssue = true,
    bool AllowsRemoteIssue = false,
    bool AllowsElectronicIssue = true,
    bool RequiresSecondVerifier = false,
    bool IsSatellite = false,
    // Pass 89: the components the location takes, so a list can say when a unit is stored where it should not be.
    bool AllowsRbc = true,
    bool AllowsPlasma = true,
    bool AllowsPlatelets = true,
    bool AllowsCryo = true,
    bool AllowsWholeBlood = true)
{
    public string ChoiceLabel => StorageLocationLabel.ForPicker(Id, Code, Name);

    /// <summary>"FRIDGE-1 does not take platelets" for a unit on this shelf whose component it does not take; else null.</summary>
    public string? WrongStorageFor(UnitStatus status, ComponentClass component) =>
        InventoryLocationPolicyRule.WrongStorage(status, Code, AllowsRbc, AllowsPlasma, AllowsPlatelets, AllowsCryo, AllowsWholeBlood, component);

    public static InventoryLocationDto From(InventoryLocation l) => new(
        l.Id, l.Code, l.Name, l.LocationType, l.IsActive,
        l.AllowsIssue, l.AllowsRemoteIssue, l.AllowsElectronicIssue, l.RequiresSecondVerifier, l.IsSatellite,
        l.AllowsRbc, l.AllowsPlasma, l.AllowsPlatelets, l.AllowsCryo, l.AllowsWholeBlood);
}

public sealed record BloodSupplierRefDto(long Id, string Code, string Name, string Registration, string DisplayName)
{
    public static BloodSupplierRefDto From(BloodSupplier supplier) => new(
        supplier.Id, supplier.Code, supplier.Name, supplier.Registration,
        BloodSupplierLabel.ForChoice(supplier.Id, supplier.Code, supplier.Name));
}

public sealed record OrderingLocationRefDto(long Id, string Code, string? Name, string? Department, bool IsActive, string DisplayName, string? Hl7MappingCode = null)
{
    public static OrderingLocationRefDto From(OrderingLocation l) => new(
        l.Id, l.Code, l.Name, l.Department, l.IsActive,
        StorageLocationLabel.Format(l.Id, l.Name), l.Hl7MappingCode);
}

public sealed record OrderingProviderRefDto(long Id, string ProviderId, string Name, string? Specialty, string? Location, bool Outside = false)
{
    public static OrderingProviderRefDto From(OrderingProvider p) => new(
        p.Id, p.ProviderId, p.Name, p.Specialty, p.Location, OutsideProviderRule.IsOutside(p.SourceSystem));
}

public sealed record TestDefinitionListItemDto(
    string Code,
    string Name,
    TestCategory Category,
    string? RequiredSpecimenType = null,
    long Id = 0)
{
    /// <summary>
    /// Name on a new or edited order. A blank name stays the test id.
    /// The code is not used as the name. The stored line name is unchanged.
    /// </summary>
    public string NameLabel => TestDefinitionLabel.Name(Id, Name);

    public static TestDefinitionListItemDto From(TestDefinition t) =>
        new(t.Code, t.Name, t.Category, t.RequiredSpecimenType, t.Id);
}

public sealed record DirectoryUserDto(string UserName, string DisplayName)
{
    public string Label =>
        string.IsNullOrWhiteSpace(DisplayName) || string.Equals(DisplayName, UserName, StringComparison.Ordinal)
            ? UserName
            : $"{DisplayName} ({UserName})";
}
