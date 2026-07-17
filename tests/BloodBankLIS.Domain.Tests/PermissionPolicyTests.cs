using BloodBankLIS.Domain.Rules;
using Xunit;

namespace BloodBankLIS.Domain.Tests;

public class PermissionPolicyTests
{
    private static IReadOnlySet<string> Set(params string[] codes) =>
        new HashSet<string>(codes, StringComparer.Ordinal);

    [Fact]
    public void NoIdentity_IsUnauthenticated()
    {
        var decision = PermissionPolicy.Evaluate(isAuthenticated: false, Set(PermissionCodes.IssueCreate), PermissionCodes.IssueCreate);
        Assert.Equal(AccessDecision.Unauthenticated, decision);
    }

    [Fact]
    public void AuthenticatedWithPermission_IsAllowed()
    {
        var decision = PermissionPolicy.Evaluate(isAuthenticated: true, Set(PermissionCodes.IssueCreate), PermissionCodes.IssueCreate);
        Assert.Equal(AccessDecision.Allowed, decision);
    }

    [Fact]
    public void AuthenticatedWithoutPermission_IsForbidden()
    {
        var decision = PermissionPolicy.Evaluate(isAuthenticated: true, Set(PermissionCodes.ResultEnter), PermissionCodes.IssueOverride);
        Assert.Equal(AccessDecision.Forbidden, decision);
    }

    [Fact]
    public void AuthenticatedWithNullPermissionSet_IsForbidden()
    {
        var decision = PermissionPolicy.Evaluate(isAuthenticated: true, grantedPermissions: null, PermissionCodes.IssueOverride);
        Assert.Equal(AccessDecision.Forbidden, decision);
    }

    [Fact]
    public void EmptyRequiredPermission_AllowsAnyAuthenticatedUser()
    {
        var decision = PermissionPolicy.Evaluate(isAuthenticated: true, Set(), requiredPermission: "");
        Assert.Equal(AccessDecision.Allowed, decision);
    }

    [Fact]
    public void Catalog_HasNoDuplicateCodes()
    {
        Assert.Equal(PermissionCodes.All.Count, PermissionCodes.All.Distinct(StringComparer.Ordinal).Count());
    }
}
