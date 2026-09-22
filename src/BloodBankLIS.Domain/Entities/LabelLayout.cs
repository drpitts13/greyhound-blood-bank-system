using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// The single active design for a specimen label or compatibility tag.
/// <see cref="DocumentJson"/> is a <c>LabelLayoutDocument</c>.
/// </summary>
public class LabelLayout : BaseEntity
{
    public LabelLayoutKind Kind { get; set; }

    public string DocumentJson { get; set; } = string.Empty;
}
