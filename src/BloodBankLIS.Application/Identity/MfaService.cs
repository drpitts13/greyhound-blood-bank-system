using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Identity;

public sealed record MfaStatusDto(bool Enrolled, DateTime? EnrolledUtc, bool Pending, bool RequiredByPolicy, bool Enabled = true);

public sealed record MfaEnrollmentDto(string SecretBase32, string ProvisioningUri);

public sealed record MfaCodeRequest(string Code);

/// <summary>
/// Authenticator-app (TOTP) enrollment for the signed-in user, and an administrator reset.
/// The key is shown once at enrollment start and stored only in protected form.
/// </summary>
public sealed class MfaService
{
    public const string Issuer = "Greyhound Blood Bank LIS";

    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly ISecretProtector _protector;
    private readonly IPermissionEvaluator _permissions;
    private readonly FacilityPolicyService _policy;

    public MfaService(
        IRepository<User> users,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        ISecretProtector protector,
        IPermissionEvaluator permissions,
        FacilityPolicyService policy)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _protector = protector;
        _permissions = permissions;
        _policy = policy;
    }

    public async Task<OperationResult<MfaStatusDto>> GetStatusAsync(CancellationToken ct = default)
    {
        var user = await CurrentAsync(ct);
        return user is null
            ? OperationResult<MfaStatusDto>.Fail("The current identity does not map to an active account.")
            : OperationResult<MfaStatusDto>.Ok(new MfaStatusDto(
                user.TotpSecretProtected is not null,
                user.TotpEnrolledUtc,
                user.TotpPendingSecretProtected is not null,
                await RequiredAsync(user, ct),
                await _policy.GetMfaEnabledAsync(ct)));
    }

    public async Task<OperationResult<MfaEnrollmentDto>> BeginEnrollmentAsync(CancellationToken ct = default)
    {
        var user = await CurrentAsync(ct);
        if (user is null)
        {
            return OperationResult<MfaEnrollmentDto>.Fail("The current identity does not map to an active account.");
        }

        if (!await _policy.GetMfaEnabledAsync(ct))
        {
            return OperationResult<MfaEnrollmentDto>.Fail(DisabledMessage);
        }

        if (user.TotpSecretProtected is not null)
        {
            return OperationResult<MfaEnrollmentDto>.Fail("An authenticator is already set up. Remove it before setting up a new one.");
        }

        var secret = Totp.NewSecret();
        var base32 = Totp.ToBase32(secret);
        user.TotpPendingSecretProtected = _protector.Protect(base32);
        _users.Update(user);
        _audit.Record(AuditEventType.Update, nameof(User), user.Id, null, new { MfaEnrollment = "Started" }, "Authenticator enrollment started.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<MfaEnrollmentDto>.Ok(new MfaEnrollmentDto(base32, Totp.ProvisioningUri(Issuer, user.UserName, secret)));
    }

    public async Task<OperationResult<MfaStatusDto>> ConfirmEnrollmentAsync(MfaCodeRequest request, CancellationToken ct = default)
    {
        var user = await CurrentAsync(ct);
        if (user is null)
        {
            return OperationResult<MfaStatusDto>.Fail("The current identity does not map to an active account.");
        }

        if (!await _policy.GetMfaEnabledAsync(ct))
        {
            return OperationResult<MfaStatusDto>.Fail(DisabledMessage);
        }

        if (user.TotpPendingSecretProtected is null)
        {
            return OperationResult<MfaStatusDto>.Fail("Start authenticator setup first.");
        }

        var pending = _protector.Unprotect(user.TotpPendingSecretProtected);
        var step = Totp.Verify(Totp.FromBase32(pending), request?.Code, _clock.UtcNow, null);
        if (step is null)
        {
            return OperationResult<MfaStatusDto>.Fail("That code does not match. Check the time on your device and try the current code.");
        }

        user.TotpSecretProtected = user.TotpPendingSecretProtected;
        user.TotpPendingSecretProtected = null;
        user.TotpEnrolledUtc = _clock.UtcNow;
        user.TotpLastStep = step;
        _users.Update(user);
        _audit.Record(AuditEventType.Update, nameof(User), user.Id, new { MfaEnrolled = false }, new { MfaEnrolled = true }, "Authenticator enrolled.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<MfaStatusDto>.Ok(new MfaStatusDto(true, user.TotpEnrolledUtc, false, await RequiredAsync(user, ct), true));
    }

    /// <summary>The user removes their own authenticator with a current code, unless policy requires it.</summary>
    public async Task<OperationResult<MfaStatusDto>> DisableAsync(MfaCodeRequest request, CancellationToken ct = default)
    {
        var user = await CurrentAsync(ct);
        if (user is null || user.TotpSecretProtected is null)
        {
            return OperationResult<MfaStatusDto>.Fail("No authenticator is set up.");
        }

        if (await RequiredAsync(user, ct))
        {
            return OperationResult<MfaStatusDto>.Fail("Facility policy requires an authenticator for your account. An administrator can reset it if your device is lost; you then set it up again at sign-in.");
        }

        var step = Totp.Verify(Totp.FromBase32(_protector.Unprotect(user.TotpSecretProtected)), request?.Code, _clock.UtcNow, user.TotpLastStep);
        if (step is null)
        {
            return OperationResult<MfaStatusDto>.Fail("That code does not match.");
        }

        Clear(user);
        _audit.Record(AuditEventType.Update, nameof(User), user.Id, new { MfaEnrolled = true }, new { MfaEnrolled = false }, "Authenticator removed by the user.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<MfaStatusDto>.Ok(new MfaStatusDto(false, null, false, false, await _policy.GetMfaEnabledAsync(ct)));
    }

    /// <summary>Administrator reset for a lost device. The endpoint requires admin.users.manage.</summary>
    public async Task<OperationResult<bool>> ResetAsync(long userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return OperationResult<bool>.Fail("User not found.");
        }

        if (string.Equals(user.UserName, _currentUser.UserName, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<bool>.Fail("Another administrator must reset your own authenticator.");
        }

        var had = user.TotpSecretProtected is not null || user.TotpPendingSecretProtected is not null;
        Clear(user);
        _audit.Record(AuditEventType.Update, nameof(User), user.Id, new { MfaEnrolled = had }, new { MfaEnrolled = false },
            $"Authenticator reset by {_currentUser.UserName}.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<bool>.Ok(true);
    }

    private void Clear(User user)
    {
        user.TotpSecretProtected = null;
        user.TotpPendingSecretProtected = null;
        user.TotpEnrolledUtc = null;
        user.TotpLastStep = null;
        _users.Update(user);
    }

    private const string DisabledMessage = "Authenticator sign-in is turned off in Facility Policies.";

    private async Task<bool> RequiredAsync(User user, CancellationToken ct) =>
        MfaRule.IsRequired(
            await _policy.GetMfaEnabledAsync(ct),
            await _policy.GetRequireAllUsersMfaAsync(ct),
            await _policy.GetRequirePrivilegedMfaAsync(ct),
            MfaRule.IsPrivileged(await _permissions.GetPermissionsAsync(user.UserName, ct)));

    private Task<User?> CurrentAsync(CancellationToken ct) =>
        _users.FirstOrDefaultAsync(u => u.UserName == _currentUser.UserName && u.IsActive && !u.IsLocked, ct);
}
