using BloodBankLIS.Domain.Common;



namespace BloodBankLIS.Domain.Entities.Configuration;



/// <summary>

/// Versioned catalog entry for an antigen and its paired antibody (e.g. K / anti-K).

/// Drives patient/unit attribute pickers and clinically significant compatibility rules.

/// </summary>

public class BloodAttributeDefinition : VersionedConfigEntity

{

    /// <summary>Antigen code (unique among active definitions), e.g. K, FYA.</summary>

    public string Code { get; set; } = string.Empty;



    /// <summary>Display name, e.g. Kell.</summary>

    public string Name { get; set; } = string.Empty;



    /// <summary>Paired antibody label, e.g. anti-K.</summary>

    public string AntibodyName { get; set; } = string.Empty;



    /// <summary>When true, compatibility rules enforce antigen-negative selection.</summary>

    public bool IsClinicallySignificant { get; set; }



    public int SortOrder { get; set; }

}

