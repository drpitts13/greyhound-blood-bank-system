using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

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

    /// <summary>
    /// Offset from collection date/time such as 72H, 3D, 2W, or 3M.
    /// Required to activate. Combined with <see cref="ExpirationMode"/>.
    /// </summary>
    public string ExpirationCode { get; set; } = string.Empty;

    /// <summary>
    /// Exact clock time versus 23:59 UTC on the final calendar day.
    /// Hours always use exact time.
    /// </summary>
    public SpecimenExpirationMode ExpirationMode { get; set; } = SpecimenExpirationMode.ExactTime;

    /// <summary>JSON array of test codes that must not be resulted on this specimen type.</summary>
    public string? ExcludedTestCodesJson { get; set; }

    public int SortOrder { get; set; }
}
