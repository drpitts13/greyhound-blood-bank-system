using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Rules;

/// <summary>Pure checks for an auto-thaw catalog row. Duplicate and missing product types are passed in.</summary>
public static class AutoThawProductValidator
{
    public static RuleEvaluation Validate(AutoThawProduct row, bool productTypeMissing, bool duplicateProductType)
    {
        var results = new List<RuleResult>();

        if (row.ProductTypeId <= 0 || productTypeMissing)
        {
            results.Add(RuleResult.HardStop(
                "AUTOTHAW.PRODUCT.MISSING",
                "Select an active product type."));
        }

        if (duplicateProductType)
        {
            results.Add(RuleResult.HardStop(
                "AUTOTHAW.PRODUCT.DUPLICATE",
                "That product code is already in the auto-thaw list."));
        }

        return new RuleEvaluation(results);
    }
}
