using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One antibody finding in an eluate (OCD-042). An alloantibody finding names a catalog antibody and posts to
/// antibody history when the workup is verified; autoantibody, panreactive, and nonspecific findings are kept on
/// the workup only.
/// </summary>
public class DatElutionEluateFinding : BaseEntity
{
    public long WorkupId { get; set; }

    public EluateFindingKind Kind { get; set; } = EluateFindingKind.Alloantibody;

    /// <summary>Catalog antibody (blood attribute) for an alloantibody finding.</summary>
    public long? BloodAttributeDefinitionId { get; set; }

    public string Specificity { get; set; } = string.Empty;

    /// <summary>False once the finding is replaced before verification. Rows are kept, never deleted.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The antibody-history row this finding created, or null when not posted (or already on file).</summary>
    public long? PostedAntibodyHistoryId { get; set; }
}
