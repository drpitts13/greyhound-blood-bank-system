using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Inventory;

/// <summary>
/// A retype-required product is confirmed only by a verified result that matches the label.
/// Products that do not require retype are treated as confirmed.
/// </summary>
public static class ProductRetypeConfirmation
{
    public static async Task<bool> IsConfirmedAsync(
        IRepository<ProductRetypeResult>? results,
        ProductType? product,
        long unitId,
        CancellationToken ct = default)
    {
        if (product?.RequiresRetype != true)
        {
            return true;
        }

        if (results is null)
        {
            return false;
        }

        return await results.AnyAsync(
            r => r.BloodProductId == unitId
                 && r.Status == ResultStatus.Verified
                 && r.MatchesLabel,
            ct);
    }
}
