using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Identity;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-303 / TEST-BB-305: TOTP as a facility option, the sign-in challenge, refused sign-in
// until enrollment when required (OCD-037), enrollment at sign-in, and reset.
public class MfaTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();
    private readonly FakeProtector _protector = new();

    public MfaTests() => _factory.Clock.UtcNow = DateTime.UnixEpoch.AddSeconds(1_780_000_000);

    public void Dispose() => _factory.Dispose();

    private AuthSessionService Sessions(BloodBankDbContext c, params string[] permissions) =>
        new(new EfRepository<User>(c),
            new EfRepository<AuthSession>(c),
            c,
            _factory.Clock,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, new StaticEnvironmentInfo("Development", false)),
            new FixedPermissionEvaluator(2, permissions.Length == 0 ? [PermissionCodes.ResultEnter] : permissions),
            new AuthSessionOptions(),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            _protector);

    private MfaService Mfa(BloodBankDbContext c, string userName, params string[] permissions) =>
        new(new EfRepository<User>(c),
            c,
            _factory.Clock,
            new TestCurrentUser(userName, "WS-1"),
            new AuditWriter(c, _factory.Clock, new TestCurrentUser(userName, "WS-1")),
            _protector,
            new FixedPermissionEvaluator(2, permissions.Length == 0 ? [PermissionCodes.ResultEnter] : permissions),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)));

    private string CurrentCode(string base32, int stepOffset = 0) =>
        Totp.Code(Totp.FromBase32(base32), Totp.StepAt(_factory.Clock.UtcNow) + stepOffset);

    private void NextStep() => _factory.Clock.UtcNow = _factory.Clock.UtcNow.AddSeconds(Totp.StepSeconds);

    private async Task<string> EnrollAsync(BloodBankDbContext c, string userName, params string[] permissions)
    {
        var enrollment = (await Mfa(c, userName, permissions).BeginEnrollmentAsync()).Value!;
        Assert.True((await Mfa(c, userName, permissions).ConfirmEnrollmentAsync(new MfaCodeRequest(CurrentCode(enrollment.SecretBase32)))).Succeeded);
        NextStep();
        return enrollment.SecretBase32;
    }

    [Fact]
    public async Task MfaOff_NoChallenge_AndSetupIsUnavailable()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);

        Assert.False((await Mfa(c, "tech1").BeginEnrollmentAsync()).Succeeded);
        await SetAsync(c, FacilityPolicyKeys.RequireAllUsersMfa, true);
        var login = await Sessions(c).LoginAsync("tech1", "demo", "BB-1");
        Assert.True(login.Value!.SignedIn);
        Assert.False((await Mfa(c, "tech1").GetStatusAsync()).Value!.Enabled);
    }

    [Fact]
    public async Task Enrolled_User_NeedsTheCode_AndAReplayedCodeFails()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        await SetAsync(c, FacilityPolicyKeys.MfaEnabled, true);
        var secret = await EnrollAsync(c, "tech1");
        var stored = await c.Users.AsNoTracking().SingleAsync(u => u.UserName == "tech1");
        Assert.DoesNotContain(secret, stored.TotpSecretProtected);

        var noCode = await Sessions(c).LoginAsync("tech1", "demo", "BB-1");
        Assert.True(noCode.Succeeded);
        Assert.False(noCode.Value!.SignedIn);
        Assert.Equal(MfaRequirement.Challenge, noCode.Value.Challenge!.Kind);
        Assert.Empty(noCode.Value.Permissions);
        Assert.Null(noCode.Value.Challenge.SecretBase32);
        Assert.Equal(0, (await c.Users.AsNoTracking().SingleAsync(u => u.UserName == "tech1")).FailedSignInCount);

        Assert.False((await Sessions(c).LoginAsync("tech1", "demo", "BB-1", "000000")).Succeeded);
        Assert.Equal(1, (await c.Users.AsNoTracking().SingleAsync(u => u.UserName == "tech1")).FailedSignInCount);

        var code = CurrentCode(secret);
        var ok = await Sessions(c).LoginAsync("tech1", "demo", "BB-1", code);
        Assert.True(ok.Value!.SignedIn, ok.Error);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EventType == AuditEventType.Login && e.NewValueJson!.Contains("TOTP")));
        Assert.False((await Sessions(c).LoginAsync("tech1", "demo", "BB-1", code)).Succeeded);

        // Turning the facility option off stops the challenge for an enrolled user.
        await SetAsync(c, FacilityPolicyKeys.MfaEnabled, false);
        Assert.True((await Sessions(c).LoginAsync("tech1", "demo", "BB-1")).Value!.SignedIn);
    }

    [Fact]
    public async Task RequiredPrivileged_IsRefusedSignIn_UntilEnrolledOnTheSignInScreen()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        await SetAsync(c, FacilityPolicyKeys.MfaEnabled, true);
        await SetAsync(c, FacilityPolicyKeys.RequirePrivilegedMfa, true);
        var admin = Sessions(c, PermissionCodes.AdminUsersManage);

        var refused = await admin.LoginAsync("supervisor", "demo", "BB-1");
        Assert.False(refused.Value!.SignedIn);
        Assert.Equal(MfaRequirement.EnrollmentRequired, refused.Value.Challenge!.Kind);
        Assert.Empty(refused.Value.Permissions);
        Assert.False(await c.AuthSessions.AnyAsync(s => s.User!.UserName == "supervisor" && s.RevokedUtc == null));
        var key = refused.Value.Challenge.SecretBase32!;
        Assert.StartsWith("otpauth://totp/", refused.Value.Challenge.ProvisioningUri);

        // A second attempt shows the same pending key, so the app entry stays valid.
        Assert.Equal(key, (await admin.LoginAsync("supervisor", "demo", "BB-1")).Value!.Challenge!.SecretBase32);

        Assert.False((await admin.LoginAsync("supervisor", "demo", "BB-1", "000000")).Succeeded);

        var signedIn = await admin.LoginAsync("supervisor", "demo", "BB-1", CurrentCode(key));
        Assert.True(signedIn.Value!.SignedIn, signedIn.Error);
        Assert.Contains(PermissionCodes.AdminUsersManage, signedIn.Value.Permissions);
        var user = await c.Users.AsNoTracking().SingleAsync(u => u.UserName == "supervisor");
        Assert.NotNull(user.TotpEnrolledUtc);
        Assert.Null(user.TotpPendingSecretProtected);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.Reason == "Authenticator enrolled at sign-in."));
        Assert.True(await c.AuditEvents.AnyAsync(e => e.Reason == "Sign-in refused until an authenticator is set up."));

        // A technologist is not in scope for the privileged-only setting.
        Assert.True((await Sessions(c, PermissionCodes.ResultEnter).LoginAsync("tech1", "demo", "BB-1")).Value!.SignedIn);
    }

    [Fact]
    public async Task RequireAllUsers_RefusesATechnologistWithoutAnAuthenticator()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        await SetAsync(c, FacilityPolicyKeys.MfaEnabled, true);
        await SetAsync(c, FacilityPolicyKeys.RequireAllUsersMfa, true);

        var refused = await Sessions(c, PermissionCodes.ResultEnter).LoginAsync("tech1", "demo", "BB-1");
        Assert.Equal(MfaRequirement.EnrollmentRequired, refused.Value!.Challenge!.Kind);
        Assert.True((await Mfa(c, "tech1").GetStatusAsync()).Value!.RequiredByPolicy);
    }

    [Fact]
    public async Task Disable_IsBlockedWhenRequired_AndAdminResetClearsIt()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        await SetAsync(c, FacilityPolicyKeys.MfaEnabled, true);
        var secret = await EnrollAsync(c, "supervisor", PermissionCodes.AdminUsersManage);
        await SetAsync(c, FacilityPolicyKeys.RequirePrivilegedMfa, true);

        var blocked = await Mfa(c, "supervisor", PermissionCodes.AdminUsersManage).DisableAsync(new MfaCodeRequest(CurrentCode(secret)));
        Assert.False(blocked.Succeeded);
        Assert.Contains("administrator", blocked.Error);

        var userId = (await c.Users.SingleAsync(u => u.UserName == "supervisor")).Id;
        Assert.False((await Mfa(c, "supervisor", PermissionCodes.AdminUsersManage).ResetAsync(userId)).Succeeded);
        Assert.True((await Mfa(c, "admin", PermissionCodes.AdminUsersManage).ResetAsync(userId)).Succeeded);
        Assert.Null((await c.Users.AsNoTracking().SingleAsync(u => u.Id == userId)).TotpSecretProtected);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityId == userId && e.Reason!.Contains("reset by admin")));

        // After the reset the policy refuses sign-in again until a new authenticator is set up.
        var refused = await Sessions(c, PermissionCodes.AdminUsersManage).LoginAsync("supervisor", "demo", "BB-1");
        Assert.Equal(MfaRequirement.EnrollmentRequired, refused.Value!.Challenge!.Kind);

        await SetAsync(c, FacilityPolicyKeys.RequirePrivilegedMfa, false);
        var techSecret = await EnrollAsync(c, "tech1");
        Assert.True((await Mfa(c, "tech1").DisableAsync(new MfaCodeRequest(CurrentCode(techSecret)))).Succeeded);
    }

    private static async Task SetAsync(BloodBankDbContext c, string key, bool on)
    {
        var row = await c.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (row is null)
        {
            c.SystemSettings.Add(new SystemSetting { Key = key, Value = on ? "true" : "false" });
        }
        else
        {
            row.Value = on ? "true" : "false";
        }

        await c.SaveChangesAsync();
    }

    /// <summary>Reversible stand-in for Data Protection that still hides the plain key.</summary>
    private sealed class FakeProtector : ISecretProtector
    {
        public string Protect(string plaintext) => "P:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));

        public string Unprotect(string protectedText) =>
            System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedText["P:".Length..]));
    }
}
