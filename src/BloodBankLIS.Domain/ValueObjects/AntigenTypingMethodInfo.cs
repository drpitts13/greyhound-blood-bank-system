namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>
/// Classifies an antigen-profile <c>Method</c> as serologic phenotype vs predicted
/// genotype for advisory comparison only. Token list is a software default
/// (OCD-024) and is not a regulatory catalog.
/// </summary>
public static class AntigenTypingMethodInfo
{
    private static readonly string[] PredictedGenotypeTokens =
    [
        "genotype",
        "molecular",
        "predicted",
        "dna",
        "pcr"
    ];

    public static bool IndicatesPredictedGenotype(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return false;
        }

        var value = method.Trim();
        return PredictedGenotypeTokens.Any(token =>
            value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}
