using System.Text.RegularExpressions;

namespace BloodBankLIS.Help.Tests;

public class ReadablePolicyKeysTests
{
    // TEST-BB-1198: every facility policy a page reads through /api/reference/policies is on that endpoint's readable
    // list. End-user testing pass 174: Compatibility read a policy that was not, the read returned 404, and the page
    // behaved as if the policy were unknown without saying so.
    [Fact]
    public void EveryPolicyThePagesRead_IsReadable()
    {
        var root = RepoRoot();
        var endpoints = File.ReadAllText(Path.Combine(root, "src", "BloodBankLIS.Api", "Endpoints", "ReferenceEndpoints.cs"));
        var start = endpoints.IndexOf("ReadablePolicyKeys = new", StringComparison.Ordinal);
        var readable = Regex.Matches(endpoints[start..endpoints.IndexOf("};", start, StringComparison.Ordinal)], @"FacilityPolicyKeys\.(\w+)")
            .Select(m => m.Groups[1].Value).ToHashSet();

        var web = Path.Combine(root, "src", "BloodBankLIS.Web");
        var missing = Directory.EnumerateFiles(web, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".razor", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal))
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"GetFacilityPolicyFlagAsync\(\s*FacilityPolicyKeys\.(\w+)")
                .Select(m => $"{Path.GetFileName(f)}: {m.Groups[1].Value}"))
            .Where(use => !readable.Contains(use[(use.IndexOf(": ", StringComparison.Ordinal) + 2)..]))
            .ToList();

        Assert.True(missing.Count == 0, "Not readable from the bench: " + string.Join("; ", missing));
        Assert.Contains("AllowElectronicCrossmatch", readable);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root.");
    }
}
