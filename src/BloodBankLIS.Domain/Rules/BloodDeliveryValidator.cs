using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

public static class BloodDeliveryValidator
{
    public const int PackingListMaxLength = 100;

    public static RuleEvaluation Validate(
        DateTime orderedUtc,
        DateTime receivedUtc,
        string? packingListNumber,
        bool supplierMissing,
        bool supplierInactive,
        IReadOnlyList<(ComponentClass ComponentClass, int ExpectedQuantity)> lines)
    {
        var results = new List<RuleResult>();

        if (supplierMissing)
        {
            results.Add(RuleResult.HardStop("DELIVERY.SUPPLIER.REQUIRED", "A blood supplier is required."));
        }
        else if (supplierInactive)
        {
            results.Add(RuleResult.HardStop("DELIVERY.SUPPLIER.INACTIVE", "The blood supplier is inactive."));
        }

        if (orderedUtc > receivedUtc.AddMinutes(1))
        {
            results.Add(RuleResult.HardStop(
                "DELIVERY.ORDERED.AFTER.RECEIPT",
                "Date/time ordered cannot be after the delivery is received."));
        }

        if (!string.IsNullOrWhiteSpace(packingListNumber) && packingListNumber.Trim().Length > PackingListMaxLength)
        {
            results.Add(RuleResult.HardStop(
                "DELIVERY.PACKINGLIST.LENGTH",
                $"Packing list number must be {PackingListMaxLength} characters or fewer."));
        }

        if (lines.Count == 0)
        {
            results.Add(RuleResult.HardStop("DELIVERY.LINES.REQUIRED", "Enter the expected number of at least one product."));
        }

        if (lines.Any(l => !Enum.IsDefined(l.ComponentClass)))
        {
            results.Add(RuleResult.HardStop("DELIVERY.LINE.PRODUCT.UNKNOWN", "One or more products were not recognized."));
        }

        if (lines.GroupBy(l => l.ComponentClass).Any(g => g.Count() > 1))
        {
            results.Add(RuleResult.HardStop("DELIVERY.LINE.DUPLICATE", "Each product can appear only once on a delivery."));
        }

        if (lines.Any(l => l.ExpectedQuantity < 1))
        {
            results.Add(RuleResult.HardStop("DELIVERY.LINE.QUANTITY", "Expected quantity must be at least 1."));
        }

        return new RuleEvaluation(results);
    }
}
