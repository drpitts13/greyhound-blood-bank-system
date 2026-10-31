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

// TEST-BB-303: TOTP enrollment, the sign-in challenge, policy-driven enrollment, and reset.
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

    [Fact]
    public async Task Enrolled_User_NeedsTheCode_AndAReplayedCodeFails()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var enrollment = (await Mfa(c, "tech1").BeginEnrollmentAsync()).Value!;
        Assert.True((await Mfa(c, "tech1").ConfirmEnrollmentAsync(new MfaCodeRequest(CurrentCode(enrollment.SecretBase32)))).Succeeded);
        var stored = await c.Users.SingleAsync(u => u.UserName == "tech1");
        Assert.NotNull(stored.TotpEnrolledUtc);
        Assert.DoesNotContain(enrollment.SecretBase32, stored.TotpSecretProtected);

        _factory.Clock.UtcNow = _factory.Clock.UtcNow.AddSeconds(60);
        var noCode = await Sessions(c).LoginAsync("tech1", "demo", "BB-1");
        Assert.False(noCode.Succeeded);
        Assert.StartsWith(AuthSessionService.MfaRequiredError, noCode.Error);
        Assert.Equal(0, (await c.Users.SingleAsync(u => u.UserName == "tech1")).FailedSignInCount);

        var wrong = await Sessions(c).LoginAsync("tech1", "demo", "BB-1", "000000");
        Assert.False(wrong.Succeeded);
        Assert.Equal(1, (await c.Users.AsNoTracking().SingleAsync(u => u.UserName == "tech1")).FailedSignInCount);

        var code = CurrentCode(enrollment.SecretBase32);
        var ok = await Sessions(c).LoginAsync("tech1", "demo", "BB-1", code);
        Assert.True(ok.Succeeded, ok.Error);
        Assert.False(ok.Value!.MfaEnrollmentRequired);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EventType == AuditEventType.Login && e.NewValueJson!.Contains("TOTP")));

        var replay = await Sessions(c).LoginAsync("tech1", "demo", "BB-1", code);
        Assert.False(replay.Succeeded);
    }

    [Fact]
    public async Task Policy_AsksPrivilegedUsersToEnroll_AndOnlyThem()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);

        var offAdmin = await Sessions(c, PermissionCodes.AdminUsersManage).LoginAsync("supervisor", "demo", "BB-1");
        Assert.False(offAdmin.Value!.MfaEnrollmentRequired);

        await SetPolicyAsync(c, true);
        var admin = await Sessions(c, PermissionCodes.AdminUsersManage).LoginAsync("supervisor", "demo", "BB-1");
        Assert.True(admin.Succeeded, admin.Error);
        Assert.True(admin.Value!.MfaEnrollmentRequired);
        var tech = await Sessions(c, PermissionCodes.ResultEnter).LoginAsync("tech1", "demo", "BB-1");
        Assert.False(tech.Value!.MfaEnrollmentRequired);

        var status = await Mfa(c, "supervisor", PermissionCodes.AdminUsersManage).GetStatusAsync();
        Assert.True(status.Value!.RequiredByPolicy);
        Assert.False(status.Value.Enrolled);
    }

    [Fact]
    public async Task Enrollment_RejectsAWrongCode_AndASecondEnrollment()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var mfa = Mfa(c, "tech1");

        Assert.False((await mfa.ConfirmEnrollmentAsync(new MfaCodeRequest("123456"))).Succeeded);
        var enrollment = (await mfa.BeginEnrollmentAsync()).Value!;
        Assert.StartsWith("otpauth://totp/", enrollment.ProvisioningUri);
        Assert.False((await mfa.ConfirmEnrollmentAsync(new MfaCodeRequest(CurrentCode(enrollment.SecretBase32, 3)))).Succeeded);
        Assert.Null((await c.Users.AsNoTracking().SingleAsync(u => u.UserName == "tech1")).TotpSecretProtected);

        Assert.True((await mfa.ConfirmEnrollmentAsync(new MfaCodeRequest(CurrentCode(enrollment.SecretBase32)))).Succeeded);
        Assert.False((await mfa.BeginEnrollmentAsync()).Succeeded);
    }

    [Fact]
    public async Task Disable_IsBlockedWhenRequired_AndAdminResetClearsIt()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var supervisor = Mfa(c, "supervisor", PermissionCodes.AdminUsersManage);
        var enrollment = (await supervisor.BeginEnrollmentAsync()).Value!;
        await supervisor.ConfirmEnrollmentAsync(new MfaCodeRequest(CurrentCode(enrollment.SecretBase32)));

        await SetPolicyAsync(c, true);
        _factory.Clock.UtcNow = _factory.Clock.UtcNow.AddSeconds(30);
        var blocked = await supervisor.DisableAsync(new MfaCodeRequest(CurrentCode(enrollment.SecretBase32)));
        Assert.False(blocked.Succeeded);
        Assert.Contains("administrator", blocked.Error);

        var userId = (await c.Users.SingleAsync(u => u.UserName == "supervisor")).Id;
        Assert.False((await Mfa(c, "supervisor", PermissionCodes.AdminUsersManage).ResetAsync(userId)).Succeeded);
        Assert.True((await Mfa(c, "admin", PermissionCodes.AdminUsersManage).ResetAsync(userId)).Succeeded);
        var reset = await c.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        Assert.Null(reset.TotpSecretProtected);
        Assert.Null(reset.TotpEnrolledUtc);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityId == userId && e.Reason!.Contains("reset by admin")));

        await SetPolicyAsync(c, false);
        var tech = Mfa(c, "tech1");
        var techEnrollment = (await tech.BeginEnrollmentAsync()).Value!;
        await tech.ConfirmEnrollmentAsync(new MfaCodeRequest(CurrentCode(techEnrollment.SecretBase32)));
        _factory.Clock.UtcNow = _factory.Clock.UtcNow.AddSeconds(30);
        Assert.True((await tech.DisableAsync(new MfaCodeRequest(CurrentCode(techEnrollment.SecretBase32)))).Succeeded);
    }

    private static async Task SetPolicyAsync(BloodBankDbContext c, bool on)
    {
        var row = await c.SystemSettings.FirstOrDefaultAsync(s => s.Key == FacilityPolicyKeys.RequirePrivilegedMfa);
        if (row is null)
        {
            c.SystemSettings.Add(new SystemSetting { Key = FacilityPolicyKeys.RequirePrivilegedMfa, Value = on ? "true" : "false" });
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
