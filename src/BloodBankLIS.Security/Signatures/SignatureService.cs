using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Identity;

namespace BloodBankLIS.Security.Signatures;

/// <summary>
/// Records and validates append-only electronic signatures. A signature is always
/// attributed to the resolved current user; it is never created on behalf of an
/// unknown account, and existing signatures are never mutated.
/// </summary>
public sealed class SignatureService : ISignatureService
{
    private readonly IRepository<User> _users;
    private readonly IRepository<ElectronicSignature> _signatures;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public SignatureService(
        IRepository<User> users,
        IRepository<ElectronicSignature> signatures,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser)
    {
        _users = users;
        _signatures = signatures;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<long>> RecordAsync(
        string action,
        string meaningOfSignature,
        string? contextType = null,
        long? contextId = null,
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

        var signature = new ElectronicSignature
        {
            UserId = user.Id,
            Action = action.Trim(),
            ContextType = string.IsNullOrWhiteSpace(contextType) ? null : contextType.Trim(),
            ContextId = contextId,
            SignedUtc = _clock.UtcNow,
            MeaningOfSignature = meaningOfSignature.Trim(),
            Workstation = _currentUser.Workstation
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
        if (signature is null || signature.UserId != user.Id)
        {
            return false;
        }

        return string.IsNullOrEmpty(expectedAction)
            || string.Equals(signature.Action, expectedAction, StringComparison.Ordinal);
    }

    private Task<User?> ResolveCurrentUserAsync(CancellationToken cancellationToken) =>
        _users.FirstOrDefaultAsync(
            u => u.UserName == _currentUser.UserName && u.IsActive && !u.IsLocked, cancellationToken);
}
