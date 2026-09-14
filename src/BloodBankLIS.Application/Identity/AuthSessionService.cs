using System.Security.Cryptography;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Identity;

public sealed class AuthSessionService : IAuthSessionService
{
    public const int FailedSignInLockoutThreshold = 5;

    private readonly IRepository<User> _users;
    private readonly IRepository<AuthSession> _sessions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator _permissions;
    private readonly AuthSessionOptions _options;

    public AuthSessionService(
        IRepository<User> users,
        IRepository<AuthSession> sessions,
        IUnitOfWork unitOfWork,
        IClock clock,
        IAuditWriter audit,
        IPermissionEvaluator permissions,
        AuthSessionOptions options)
    {
        _users = users;
        _sessions = sessions;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _audit = audit;
        _permissions = permissions;
        _options = options;
    }

    public async Task<OperationResult<AuthLoginResult>> LoginAsync(
        string userName,
        string? password,
        string? workstation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return OperationResult<AuthLoginResult>.Fail("Sign-in failed.");
        }

        var user = await _users.FirstOrDefaultAsync(u => u.UserName == userName.Trim(), cancellationToken);
        if (user is null || !user.IsActive || user.IsServiceAccount)
        {
            return OperationResult<AuthLoginResult>.Fail("Sign-in failed.");
        }

        if (user.IsLocked)
        {
            _audit.Record(
                AuditEventType.Lockout,
                nameof(User),
                user.Id,
                reason: "Locked account sign-in attempt",
                actingUserName: user.UserName);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return OperationResult<AuthLoginResult>.Fail("Sign-in failed.");
        }

        if (string.IsNullOrEmpty(user.PasswordHash)
            || !SecretHasher.Verify(password ?? string.Empty, user.PasswordHash))
        {
            user.FailedSignInCount++;
            if (user.FailedSignInCount >= FailedSignInLockoutThreshold)
            {
                user.IsLocked = true;
                _audit.Record(
                    AuditEventType.Lockout,
                    nameof(User),
                    user.Id,
                    reason: "Failed sign-in lockout",
                    actingUserName: user.UserName);
                await RevokeOpenSessionsAsync(user.Id, "Account locked after failed sign-in", cancellationToken);
            }
            else
            {
                _audit.Record(
                    AuditEventType.SignatureFailed,
                    nameof(User),
                    user.Id,
                    reason: "Failed sign-in",
                    actingUserName: user.UserName);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return OperationResult<AuthLoginResult>.Fail("Sign-in failed.");
        }

        var now = _clock.UtcNow;
        var idle = TimeSpan.FromMinutes(Math.Max(1, _options.IdleTimeoutMinutes));
        var absolute = TimeSpan.FromHours(Math.Max(1, _options.AbsoluteTimeoutHours));
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var session = new AuthSession
        {
            UserId = user.Id,
            TokenHash = SecretHasher.HashOpaqueToken(rawToken),
            IssuedUtc = now,
            LastActivityUtc = now,
            AbsoluteExpiresUtc = now.Add(absolute),
            IdleExpiresUtc = now.Add(idle),
            Workstation = string.IsNullOrWhiteSpace(workstation) ? null : workstation.Trim()
        };

        await _sessions.AddAsync(session, cancellationToken);
        user.LastLoginUtc = now;
        user.FailedSignInCount = 0;
        _audit.Record(
            AuditEventType.Login,
            nameof(User),
            user.Id,
            newValue: new { session.Workstation, AbsoluteExpiresUtc = session.AbsoluteExpiresUtc },
            actingUserName: user.UserName);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var codes = await _permissions.GetPermissionsAsync(user.UserName, cancellationToken);
        var securityLevel = await _permissions.GetMaxSecurityLevelAsync(user.UserName, cancellationToken);
        return OperationResult<AuthLoginResult>.Ok(new AuthLoginResult(
            rawToken,
            user.UserName,
            user.DisplayName,
            securityLevel,
            codes.OrderBy(c => c).ToArray()));
    }

    public async Task<OperationResult<bool>> LogoutAsync(string? token, CancellationToken cancellationToken = default)
    {
        var session = await FindByTokenAsync(token, cancellationToken);
        if (session is null)
        {
            return OperationResult<bool>.Ok(true);
        }

        if (session.RevokedUtc is null)
        {
            session.RevokedUtc = _clock.UtcNow;
            session.RevokedReason = "Logout";
            _audit.Record(
                AuditEventType.Logout,
                nameof(User),
                session.UserId,
                newValue: new { SessionId = session.Id },
                actingUserName: session.User?.UserName);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return OperationResult<bool>.Ok(true);
    }

    public async Task<AuthSessionPrincipal?> ResolveAsync(string? token, CancellationToken cancellationToken = default)
    {
        var session = await FindByTokenAsync(token, cancellationToken);
        if (session?.User is null)
        {
            return null;
        }

        var check = AuthSessionValidityRule.Evaluate(
            _clock.UtcNow,
            session.RevokedUtc,
            session.AbsoluteExpiresUtc,
            session.IdleExpiresUtc,
            session.User.IsActive,
            session.User.IsLocked);

        return check.Severity == RuleSeverity.Pass
            ? new AuthSessionPrincipal(session.Id, session.UserId, session.User.UserName, session.Workstation)
            : null;
    }

    public async Task TouchAsync(long sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || session.RevokedUtc is not null)
        {
            return;
        }

        var now = _clock.UtcNow;
        if (now - session.LastActivityUtc < TimeSpan.FromMinutes(1))
        {
            return;
        }

        var idle = TimeSpan.FromMinutes(Math.Max(1, _options.IdleTimeoutMinutes));
        session.LastActivityUtc = now;
        session.IdleExpiresUtc = now.Add(idle);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeUserSessionsAsync(long userId, string reason, CancellationToken cancellationToken = default)
    {
        await RevokeOpenSessionsAsync(userId, reason, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeOpenSessionsAsync(long userId, string reason, CancellationToken cancellationToken)
    {
        var open = await _sessions.ListAsync(
            s => s.UserId == userId && s.RevokedUtc == null,
            cancellationToken);
        var now = _clock.UtcNow;
        foreach (var session in open)
        {
            var tracked = await _sessions.GetByIdAsync(session.Id, cancellationToken);
            if (tracked is null || tracked.RevokedUtc is not null)
            {
                continue;
            }

            tracked.RevokedUtc = now;
            tracked.RevokedReason = reason;
        }
    }

    private async Task<AuthSession?> FindByTokenAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = SecretHasher.HashOpaqueToken(token);
        var session = await _sessions.FirstOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);
        if (session is null)
        {
            return null;
        }

        if (session.User is null)
        {
            session.User = await _users.GetByIdAsync(session.UserId, cancellationToken);
        }

        return session;
    }
}
