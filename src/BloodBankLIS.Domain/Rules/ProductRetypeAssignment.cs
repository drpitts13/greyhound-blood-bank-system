using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Resolves the catalog retype test for a product from the unit's labeled Rh.
/// </summary>
public static class ProductRetypeAssignment
{
    public const string RhPositiveTestCode = "ABORH-RETYPE-POS";
    public const string RhNegativeTestCode = "ABORH-RETYPE-NEG";
    public const string MissingRhCode = "RETYPE.RH.REQUIRED";
    public const string MissingTestCode = "RETYPE.TEST.REQUIRED";

    public static long? ResolveTestId(ProductType product, RhType labeledRh)
    {
        ArgumentNullException.ThrowIfNull(product);
        return labeledRh switch
        {
            RhType.Positive => product.RhPositiveRetypeTestId,
            RhType.Negative => product.RhNegativeRetypeTestId,
            _ => null
        };
    }

    public static RuleResult? Evaluate(ProductType product, RhType labeledRh)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (!product.RequiresRetype)
        {
            return null;
        }

        if (labeledRh is not (RhType.Positive or RhType.Negative))
        {
            return RuleResult.HardStop(
                MissingRhCode,
                "A labeled Rh type is required to add the applicable retype test.");
        }

        if (ResolveTestId(product, labeledRh) is null)
        {
            var slot = labeledRh == RhType.Positive ? "Rh+ Retype Test" : "Rh− Retype Test";
            return RuleResult.HardStop(
                MissingTestCode,
                $"Product '{product.ProductCode}' requires retype but {slot} is not configured.");
        }

        return null;
    }
}
