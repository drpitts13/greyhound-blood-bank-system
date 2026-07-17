namespace BloodBankLIS.Domain.Rules;

public static class OrderingProviderValidator
{
    public static RuleEvaluation Validate(Entities.OrderingProvider p, bool duplicateProviderId)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(p.ProviderId))
        {
            results.Add(RuleResult.HardStop("PROVIDER.ID.REQUIRED", "Provider id is required."));
        }

        if (string.IsNullOrWhiteSpace(p.Name))
        {
            results.Add(RuleResult.HardStop("PROVIDER.NAME.REQUIRED", "Provider name is required."));
        }

        if (duplicateProviderId)
        {
            results.Add(RuleResult.HardStop("PROVIDER.ID.DUPLICATE", $"Another provider already uses id '{p.ProviderId}'."));
        }

        return new RuleEvaluation(results);
    }
}
