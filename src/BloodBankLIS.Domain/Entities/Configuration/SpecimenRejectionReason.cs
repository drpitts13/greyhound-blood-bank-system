using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Admin catalog of why a specimen may be rejected. Rejection stores the code on the specimen so rejections can be
/// counted by cause. <see cref="RequiresComment"/> makes the comment mandatory, for a catch-all code such as Other.
/// The starter list is an example for the facility to edit, not a regulatory list.
/// </summary>
public class SpecimenRejectionReason : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool RequiresComment { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
