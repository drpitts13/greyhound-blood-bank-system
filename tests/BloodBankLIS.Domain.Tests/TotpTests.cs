using System.Text;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-303: RFC 6238 TOTP, base32, and who must use MFA.
public class TotpTests
{
    // RFC 6238 appendix B SHA-1 seed; the published codes are 8 digits, so a 6-digit code is the last six.
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void Code_MatchesRfc6238Vectors(long unixSeconds, string expected)
    {
        var step = Totp.StepAt(DateTime.UnixEpoch.AddSeconds(unixSeconds));
        Assert.Equal(expected, Totp.Code(RfcSecret, step));
    }

    [Fact]
    public void Verify_AcceptsOneStepEitherSide_AndRejectsFurther()
    {
        var now = DateTime.UnixEpoch.AddSeconds(1234567890);
        var step = Totp.StepAt(now);

        Assert.Equal(step, Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step), now, null));
        Assert.Equal(step - 1, Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step - 1), now, null));
        Assert.Equal(step + 1, Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step + 1), now, null));
        Assert.Null(Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step - 2), now, null));
        Assert.Null(Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step + 2), now, null));
    }

    [Fact]
    public void Verify_RejectsReplay_AndMalformedCodes()
    {
        var now = DateTime.UnixEpoch.AddSeconds(1234567890);
        var step = Totp.StepAt(now);
        var code = Totp.Code(RfcSecret, step);

        Assert.Null(Totp.Verify(RfcSecret, code, now, lastUsedStep: step));
        Assert.Equal(step, Totp.Verify(RfcSecret, code, now, lastUsedStep: step - 1));
        Assert.Equal(step, Totp.Verify(RfcSecret, " " + code[..3] + " " + code[3..], now, null));
        Assert.Null(Totp.Verify(RfcSecret, "12345", now, null));
        Assert.Null(Totp.Verify(RfcSecret, "12345a", now, null));
        Assert.Null(Totp.Verify(RfcSecret, null, now, null));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Base32_MatchesRfc4648(string text, string expected)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        Assert.Equal(expected, Totp.ToBase32(bytes));
        Assert.Equal(bytes, Totp.FromBase32(expected));
    }

    [Fact]
    public void NewSecret_RoundTripsThroughBase32_AndBuildsAProvisioningUri()
    {
        var secret = Totp.NewSecret();
        Assert.Equal(Totp.SecretBytes, secret.Length);
        Assert.Equal(secret, Totp.FromBase32(Totp.ToBase32(secret).ToLowerInvariant()));
        var uri = Totp.ProvisioningUri("Greyhound Blood Bank LIS", "tech1", secret);
        Assert.StartsWith("otpauth://totp/Greyhound%20Blood%20Bank%20LIS:tech1?secret=", uri);
        Assert.Contains("&digits=6&period=30", uri);
        Assert.Throws<FormatException>(() => Totp.FromBase32("ABC1"));
    }

    [Theory]
    [InlineData(false, false, false, MfaRequirement.None)]
    [InlineData(false, true, true, MfaRequirement.None)]
    [InlineData(true, false, false, MfaRequirement.None)]
    [InlineData(true, true, false, MfaRequirement.EnrollmentRequired)]
    [InlineData(true, false, true, MfaRequirement.Challenge)]
    [InlineData(true, true, true, MfaRequirement.Challenge)]
    public void MfaRule_IsAFacilityOption_EnrolledChallenges_RequiredMustEnroll(
        bool enabled, bool required, bool enrolled, MfaRequirement expected)
    {
        Assert.Equal(expected, MfaRule.Evaluate(enabled, required, enrolled));
    }

    [Theory]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, false, true, true, true)]
    [InlineData(true, true, false, false, true)]
    public void IsRequired_FollowsTheFacilitySettings(bool enabled, bool all, bool privilegedPolicy, bool privileged, bool expected)
    {
        Assert.Equal(expected, MfaRule.IsRequired(enabled, all, privilegedPolicy, privileged));
    }

    [Fact]
    public void Privileged_MeansAdministrationOrOverrideApproval()
    {
        Assert.True(MfaRule.IsPrivileged([PermissionCodes.ResultEnter, PermissionCodes.AdminUsersManage]));
        Assert.True(MfaRule.IsPrivileged([PermissionCodes.OverrideApprove]));
        Assert.False(MfaRule.IsPrivileged([PermissionCodes.ResultEnter, PermissionCodes.IssueCreate]));
    }
}
