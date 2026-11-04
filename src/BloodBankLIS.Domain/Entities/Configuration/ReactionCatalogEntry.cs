using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// OCD-057 (decided 2026-11-04): the reactions a provider may conclude, and the treatments (follow-up for future
/// transfusions) they may choose, are admin lists. Each list always has a "none" entry (No reaction, No treatment)
/// that cannot be removed. The facility maintains the rest.
/// </summary>
public class ReactionCatalogEntry : BaseEntity
{
    public ReactionCatalogKind Kind { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>The fixed No reaction / No treatment entry: always active, code fixed.</summary>
    public bool IsNoneOption { get; set; }
}
