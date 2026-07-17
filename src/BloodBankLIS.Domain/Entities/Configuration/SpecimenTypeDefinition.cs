using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Versioned catalog of specimen types used at accessioning and for test compatibility.
/// </summary>
public class SpecimenTypeDefinition : VersionedConfigEntity
{
    /// <summary>Unique code among active definitions, e.g. EDTA, SERUM.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display description shown in pick lists, e.g. EDTA Whole Blood.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>JSON array of test codes that must not be resulted on this specimen type.</summary>
    public string? ExcludedTestCodesJson { get; set; }

    public int SortOrder { get; set; }
}
