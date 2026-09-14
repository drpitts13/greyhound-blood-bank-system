using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Integration.Tests.SafetyRegression;

/// <summary>
/// A self-asserted user-name header must not authenticate an operator.
/// Impersonating supervisor would otherwise unlock emergency release, override,
/// and patient merge.
/// </summary>
public class IdentitySpoofingRegressionTests
{
    [Fact]
    public void SpoofedUserHeader_WithoutSession_IsAnonymous()
    {
        var identity = RequestIdentityResolver.Resolve(
            session: null,
            legacyUserHeader: "supervisor",
            legacyWorkstationHeader: "WARD-1",
            allowLegacyIdentityHeader: false,
            devModeEnabled: false,
            devModeUserName: "DEV_ADMIN");

        Assert.False(identity.IsAuthenticated);
        Assert.Equal("system", identity.UserName);
    }

    [Fact]
    public void SpoofedUserHeader_DoesNotOverrideValidSession()
    {
        var session = new AuthSessionPrincipal(9, 3, "tech1", "BB-1");
        var identity = RequestIdentityResolver.Resolve(
            session,
            legacyUserHeader: "supervisor",
            legacyWorkstationHeader: "ATTACKER",
            allowLegacyIdentityHeader: false,
            devModeEnabled: false,
            devModeUserName: "DEV_ADMIN");

        Assert.True(identity.IsAuthenticated);
        Assert.Equal("tech1", identity.UserName);
        Assert.Equal("BB-1", identity.Workstation);
    }

    [Fact]
    public void LegacyHeader_OnlyWhenExplicitlyAllowed()
    {
        var allowed = RequestIdentityResolver.Resolve(
            null, "supervisor", "WARD-1", allowLegacyIdentityHeader: true, false, "DEV_ADMIN");
        Assert.True(allowed.IsAuthenticated);
        Assert.Equal("supervisor", allowed.UserName);

        var denied = RequestIdentityResolver.Resolve(
            null, "supervisor", "WARD-1", allowLegacyIdentityHeader: false, false, "DEV_ADMIN");
        Assert.False(denied.IsAuthenticated);
    }

    [Fact]
    public void DevMode_AuthenticatesConfiguredUser_OnlyWhenEnabled()
    {
        var on = RequestIdentityResolver.Resolve(null, null, null, false, true, "DEV_ADMIN");
        Assert.True(on.IsAuthenticated);
        Assert.Equal("DEV_ADMIN", on.UserName);

        var off = RequestIdentityResolver.Resolve(null, null, null, false, false, "DEV_ADMIN");
        Assert.False(off.IsAuthenticated);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Basic abc")]
    [InlineData("Bearer")]
    public void ReadBearerToken_RejectsMissingOrNonBearer(string? header)
    {
        Assert.Null(RequestIdentityResolver.ReadBearerToken(header));
    }

    [Fact]
    public void ReadBearerToken_ReadsSchemeValue()
    {
        Assert.Equal("tok-1", RequestIdentityResolver.ReadBearerToken("Bearer tok-1"));
    }
}
