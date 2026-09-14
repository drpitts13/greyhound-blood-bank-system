using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Identity;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Abstractions;

public interface IAuthSessionService
{
    Task<OperationResult<AuthLoginResult>> LoginAsync(
        string userName,
        string? password,
        string? workstation,
        CancellationToken cancellationToken = default);

    Task<OperationResult<bool>> LogoutAsync(string? token, CancellationToken cancellationToken = default);

    Task<AuthSessionPrincipal?> ResolveAsync(string? token, CancellationToken cancellationToken = default);

    Task TouchAsync(long sessionId, CancellationToken cancellationToken = default);

    Task RevokeUserSessionsAsync(long userId, string reason, CancellationToken cancellationToken = default);
}
