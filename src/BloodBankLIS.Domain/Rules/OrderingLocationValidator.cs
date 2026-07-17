namespace BloodBankLIS.Domain.Rules;

public static class OrderingLocationValidator
{
    public static RuleEvaluation Validate(Entities.OrderingLocation location, bool duplicateCode)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(location.Code))
        {
            results.Add(RuleResult.HardStop("LOCATION.CODE.REQUIRED", "Location code is required."));
        }

        if (duplicateCode)
        {
            results.Add(RuleResult.HardStop("LOCATION.CODE.DUPLICATE", $"Another location already uses code '{location.Code}'."));
        }

        return new RuleEvaluation(results);
    }
}
