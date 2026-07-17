using BloodBankLIS.Application.Abstractions;

namespace BloodBankLIS.Infrastructure.Common;

/// <summary>Production clock returning the current UTC time.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
