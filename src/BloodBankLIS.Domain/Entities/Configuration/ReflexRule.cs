using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Versioned reflex mapping: when a trigger test verifies with a matching result value,
/// the reflex test is added as an order line on the same order/specimen.
/// </summary>
public class ReflexRule : VersionedConfigEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string TriggerTestCode { get; set; } = string.Empty;

    /// <summary>Normalized match against <c>TestResult.Value</c> (trim + case-insensitive).</summary>
    public string TriggerResultValue { get; set; } = string.Empty;

    public string ReflexTestCode { get; set; } = string.Empty;
}
