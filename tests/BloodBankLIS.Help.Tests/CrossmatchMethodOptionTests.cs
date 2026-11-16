using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class CrossmatchMethodOptionTests
{
    // TEST-BB-1197: Record crossmatch does not offer Electronic while the facility has electronic crossmatch off
    // (end-user testing pass 174); an unread policy leaves it to the server.
    [Fact]
    public void For_ElectronicIsUnavailableWhenTheFacilityHasItOff()
    {
        Assert.Equal(("Electronic (off at this facility)", true), CrossmatchMethodOption.For(CrossmatchMethod.Electronic, "Electronic", false));
        Assert.Equal(("Electronic", false), CrossmatchMethodOption.For(CrossmatchMethod.Electronic, "Electronic", true));
        Assert.Equal(("Electronic", false), CrossmatchMethodOption.For(CrossmatchMethod.Electronic, "Electronic", null));
        Assert.Equal(("Serologic", false), CrossmatchMethodOption.For(CrossmatchMethod.Serologic, "Serologic", false));

        var page = File.ReadAllText(Path.Combine(RepoRoot(), "src", "BloodBankLIS.Web", "Components", "Pages", "Compatibility.razor"));
        Assert.Contains("CrossmatchMethodOption.For(m, Ui.EnumText(m), _facilityAllowsExm)", page);

        // The bench can read the policy: it is on the reference endpoint's readable list (otherwise the read 404s and the
        // option stays offered, as found live).
        var endpoints = File.ReadAllText(Path.Combine(RepoRoot(), "src", "BloodBankLIS.Api", "Endpoints", "ReferenceEndpoints.cs"));
        var readable = endpoints[endpoints.IndexOf("ReadablePolicyKeys = new", StringComparison.Ordinal)..endpoints.IndexOf("};", endpoints.IndexOf("ReadablePolicyKeys = new", StringComparison.Ordinal), StringComparison.Ordinal)];
        Assert.Contains("FacilityPolicyKeys.AllowElectronicCrossmatch,", readable);
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
