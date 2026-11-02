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
    /// <summary>
    /// The most recent verified retype disagrees with the label: the unit is known to be mislabeled. Unlike a unit
    /// that is simply not yet retyped, it must not leave the blood bank on any issue type.
    /// </summary>
    public static async Task<bool> HasVerifiedMismatchAsync(
        IRepository<ProductRetypeResult>? results,
        long unitId,
        CancellationToken ct = default)
    {
        if (results is null)
        {
            return false;
        }

        var latest = (await results.ListAsync(r => r.BloodProductId == unitId && r.Status == ResultStatus.Verified, ct))
            .OrderByDescending(r => r.VerifiedUtc ?? r.CreatedUtc)
            .ThenByDescending(r => r.Id)
            .FirstOrDefault();
        return latest is { MatchesLabel: false };
    }

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
