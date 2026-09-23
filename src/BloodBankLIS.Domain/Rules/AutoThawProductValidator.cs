using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Rules;

/// <summary>Pure checks for an auto-thaw catalog row. Catalog and duplicate flags are passed in.</summary>
public static class AutoThawProductValidator
{
    public static RuleEvaluation Validate(AutoThawProduct row, bool codeUnknown, bool codeRetired, bool duplicateCode)
    {
        var results = new List<RuleResult>();
        var code = (row.ProductCode ?? string.Empty).Trim().ToUpperInvariant();

        if (code.Length != 5)
        {
            results.Add(RuleResult.HardStop(
                "AUTOTHAW.PRODUCT.MISSING",
                "Enter a five-character ISBT product code such as E0707."));
        }
        else if (codeUnknown)
        {
            results.Add(RuleResult.HardStop(
                "AUTOTHAW.PRODUCT.MISSING",
                $"Product code '{code}' is not in the ISBT product code catalog."));
        }
        else if (codeRetired)
        {
            results.Add(RuleResult.HardStop(
                "AUTOTHAW.PRODUCT.RETIRED",
                $"Product code '{code}' is retired."));
        }

        if (duplicateCode)
        {
            results.Add(RuleResult.HardStop(
                "AUTOTHAW.PRODUCT.DUPLICATE",
                "That product code is already in the auto-thaw list."));
        }

        return new RuleEvaluation(results);
    }
}
