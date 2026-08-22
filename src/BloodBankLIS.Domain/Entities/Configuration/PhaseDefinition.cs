using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Reusable catalog entry for a reaction phase (e.g. IS, 37°C, AHG, check cells).
/// Assigned per panel subtest; check-cell phases are excluded from interpretation.
/// </summary>
public class PhaseDefinition : VersionedConfigEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>When false, the phase is captured at entry but omitted from interpretation columns.</summary>
    public bool IncludeInInterpretation { get; set; } = true;

    public bool IsCheckCell { get; set; }

    /// <summary>Phase this check-cell result validates (typically AHG).</summary>
    public string? ValidatesPhaseCode { get; set; }
}
