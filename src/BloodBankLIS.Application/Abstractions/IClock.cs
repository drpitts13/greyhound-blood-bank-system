namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Abstraction over the system clock so time-dependent rules (expiration, windows)
/// are deterministic and testable. All times are UTC.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
