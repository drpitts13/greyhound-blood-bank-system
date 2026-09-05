using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>Versioned reagent-panel manufacturer (or in-house panel source).</summary>
public class AntibodyPanelManufacturer : VersionedConfigEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
