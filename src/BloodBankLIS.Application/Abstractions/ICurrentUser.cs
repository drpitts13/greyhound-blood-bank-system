namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Identifies the actor for the current operation. Used to stamp audit metadata
/// and audit events. Resolved per request/scope in the composition root.
/// </summary>
public interface ICurrentUser
{
    string UserName { get; }

    string? Workstation { get; }
}
