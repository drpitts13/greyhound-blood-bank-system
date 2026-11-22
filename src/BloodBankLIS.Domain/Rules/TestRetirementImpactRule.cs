namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// What still uses a test being retired (end-user testing loop 2 tick 22): active groupers that order it, active reflex
/// rules that trigger on it or add it, and active products that retype with it. Retiring is not refused (the facility may
/// be replacing the test); each use is named so it can be updated.
/// </summary>
public static class TestRetirementImpactRule
{
    public const string Code = "TEST-RETIRE-IN-USE";

    public sealed record Grouper(string Code, IReadOnlyList<string> MemberCodes);

    public sealed record Reflex(string Code, string TriggerTestCode, string ReflexTestCode);

    public sealed record Product(string Code, bool RetypesWithIt);

    public static IReadOnlyList<RuleResult> Warnings(
        string testCode,
        IEnumerable<Grouper> activeGroupers,
        IEnumerable<Reflex> activeReflexRules,
        IEnumerable<Product> activeProducts)
    {
        var code = testCode.Trim();
        bool Same(string other) => string.Equals(other?.Trim(), code, StringComparison.OrdinalIgnoreCase);

        var results = new List<RuleResult>();
        var groupers = activeGroupers.Where(g => g.MemberCodes.Any(Same)).Select(g => g.Code).OrderBy(c => c).ToList();
        if (groupers.Count > 0)
        {
            results.Add(RuleResult.Warning(Code,
                $"{code} is still a member of {Plural(groupers.Count, "test grouper")} ({string.Join(", ", groupers)}); orders for {(groupers.Count == 1 ? "it" : "them")} will no longer include {code}. Update {(groupers.Count == 1 ? "it" : "them")} on Test Groupers."));
        }

        var reflexes = activeReflexRules.Where(r => Same(r.TriggerTestCode) || Same(r.ReflexTestCode)).Select(r => r.Code).OrderBy(c => c).ToList();
        if (reflexes.Count > 0)
        {
            results.Add(RuleResult.Warning(Code,
                $"{code} is still used by {Plural(reflexes.Count, "reflex rule")} ({string.Join(", ", reflexes)}). Update or deactivate {(reflexes.Count == 1 ? "it" : "them")} on Reflex Rules."));
        }

        var products = activeProducts.Where(p => p.RetypesWithIt).Select(p => p.Code).OrderBy(c => c).ToList();
        if (products.Count > 0)
        {
            results.Add(RuleResult.Warning(Code,
                $"{code} is still the retype test for {Plural(products.Count, "product")} ({string.Join(", ", products)}). Choose another retype test on Products."));
        }

        return results;
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
