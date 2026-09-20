using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Catalog of billable clinical actions. Modifications are the first category;
/// an order-linked action of category <see cref="ActionCategory.Modification"/>
/// maps to a 1:1 <see cref="ModificationType"/>.
/// </summary>
public class ActionDefinition : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ActionCategory Category { get; set; } = ActionCategory.Modification;

    public ModificationType? ModificationType { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Monotonic config version; bumped on significant admin edits.</summary>
    public int Version { get; set; } = 1;
}
