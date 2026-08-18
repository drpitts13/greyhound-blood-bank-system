using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Security.Signatures;

/// <summary>
/// Records and validates append-only electronic signatures. A signature is always
/// attributed to the resolved current user; it is never created on behalf of an
/// unknown account, and existing signatures are never mutated except consumption.
/// </summary>
public sealed class SignatureService : ISignatureService
{
    private readonly IRepository<User> _users;
    private readonly IRepository<ElectronicSignature> _signatures;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IEnvironmentInfo? _environment;
    private readonly IAuditWriter? _audit;

    public SignatureService(
        IRepository<User> users,
        IRepository<ElectronicSignature> signatures,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IEnvironmentInfo? environment = null,
        IAuditWriter? audit = null)
    {
        _users = users;
        _signatures = signatures;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _environment = environment;
        _audit = audit;
    }

    public async Task<OperationResult<long>> RecordAsync(
        string action,
        string meaningOfSignature,
        string? contextType = null,
        long? contextId = null,
        string? reauthenticationSecret = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return OperationResult<long>.Fail("An action is required for an electronic signature.");
        }

        if (string.IsNullOrWhiteSpace(meaningOfSignature))
        {
            return OperationResult<long>.Fail("A meaning-of-signature is required.");
        }

        var user = await ResolveCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return OperationResult<long>.Fail("The current user could not be resolved to an active account.");
        }

        var auth = Authenticate(user, reauthenticationSecret);
        if (!auth.Succeeded)
        {
            _audit?.Record(
                AuditEventType.SignatureFailed,
                nameof(ElectronicSignature),
                user.Id,
                reason: auth.Error);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return OperationResult<long>.Fail(auth.Error ?? "Re-authentication failed.");
        }

        var signedUtc = _clock.UtcNow;
        var signature = new ElectronicSignature
        {
            UserId = user.Id,
            Action = action.Trim(),
            ContextType = string.IsNullOrWhiteSpace(contextType) ? null : contextType.Trim(),
            ContextId = contextId,
            SignedUtc = signedUtc,
            MeaningOfSignature = meaningOfSignature.Trim(),
            Workstation = _currentUser.Workstation,
            AuthenticationMethod = auth.Method,
            SignatureHash = SecretHasher.ComputeSignatureHash(
                action.Trim(), meaningOfSignature.Trim(), user.UserName, contextType, contextId, signedUtc),
            ExpiresUtc = signedUtc.AddMinutes(15)
        };

        await _signatures.AddAsync(signature, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperationResult<long>.Ok(signature.Id);
    }

    public async Task<bool> IsValidForCurrentUserAsync(
        long signatureId,
        string? expectedAction = null,
        CancellationToken cancellationToken = default)
    {
        if (signatureId <= 0)
        {
            return false;
        }

        var user = await ResolveCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return false;
        }

        var signature = await _signatures.FirstOrDefaultAsync(s => s.Id == signatureId, cancellationToken);
        if (signature is null || signature.UserId != user.Id || signature.ConsumedUtc is not null)
        {
            return false;
        }

        if (signature.ExpiresUtc is not null && signature.ExpiresUtc.Value < _clock.UtcNow)
        {
            return false;
        }

        return string.IsNullOrEmpty(expectedAction)
            || string.Equals(signature.Action, expectedAction, StringComparison.Ordinal);
    }

    public async Task ConsumeAsync(long signatureId, CancellationToken cancellationToken = default)
    {
        var signature = await _signatures.GetByIdAsync(signatureId, cancellationToken);
        if (signature is null || signature.ConsumedUtc is not null)
        {
            return;
        }

        signature.ConsumedUtc = _clock.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static (bool Succeeded, string? Error, ElectronicSignatureAuthenticationMethod Method) Authenticate(
        User user, string? secret)
    {
        if (!string.IsNullOrEmpty(user.PasswordHash) || !string.IsNullOrEmpty(user.PinHash))
        {
            var passwordOk = SecretHasher.Verify(secret ?? string.Empty, user.PasswordHash);
            var pinOk = SecretHasher.Verify(secret ?? string.Empty, user.PinHash);
            if (!passwordOk && !pinOk)
            {
                return (false, "Password or PIN re-authentication is required to sign.", ElectronicSignatureAuthenticationMethod.Password);
            }

            return (true, null, pinOk && !passwordOk
                ? ElectronicSignatureAuthenticationMethod.Pin
                : ElectronicSignatureAuthenticationMethod.Password);
        }

        return (true, null, ElectronicSignatureAuthenticationMethod.FederatedStepUp);
    }

    private Task<User?> ResolveCurrentUserAsync(CancellationToken cancellationToken) =>
        _users.FirstOrDefaultAsync(
            u => u.UserName == _currentUser.UserName && u.IsActive && !u.IsLocked, cancellationToken);
}
