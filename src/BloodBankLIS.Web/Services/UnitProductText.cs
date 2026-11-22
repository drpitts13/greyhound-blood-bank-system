using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// A unit's product by name from its product type, "FFP — Fresh Frozen Plasma" (end-user testing pass 307: the Modify
/// page's selected unit showed "Product code —" for a unit received without an ISBT product code, so the product being
/// thawed or divided was not named anywhere on the card).
/// </summary>
public static class UnitProductText
{
    public static string? For(IEnumerable<ProductTypeDto> productTypes, long productTypeId) =>
        productTypes.FirstOrDefault(p => p.Id == productTypeId) is { } type
            ? ProductTypeLabel.ForChoice(type.Id, type.ProductCode, type.Name)
            : null;
}
