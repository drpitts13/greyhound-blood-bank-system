using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Facility policy value (specimen windows, dual-verification, retention). Never
/// hard-code clinical intervals in the engine; read these keys at the application edge.
/// </summary>
public class SystemSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Category { get; set; }

    public string? Description { get; set; }

    public bool LegalHold { get; set; }
}
