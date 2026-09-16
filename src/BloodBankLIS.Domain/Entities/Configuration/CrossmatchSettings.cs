using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Singleton, versioned crossmatch defaults: serologic tests by antibody history
/// and electronic XM candidate minimums (AABB 5.16). Edits are audited; the row
/// is never deleted.
/// </summary>
public class CrossmatchSettings : VersionedConfigEntity
{
    public const string DefaultNegativeTestCode = "XM";
    public const string DefaultPositiveTestCode = "CXM";
    public const string DefaultElectronicTestCode = "EXM";
    public const int DefaultElectronicMinimum = 2;

    public string NegativeAntibodyHistoryTestCode { get; set; } = DefaultNegativeTestCode;

    public string PositiveAntibodyHistoryTestCode { get; set; } = DefaultPositiveTestCode;

    public string ElectronicCrossmatchTestCode { get; set; } = DefaultElectronicTestCode;

    public int ElectronicXmMinimumVisits { get; set; } = DefaultElectronicMinimum;

    public int ElectronicXmMinimumSpecimens { get; set; } = DefaultElectronicMinimum;

    public int ElectronicXmMinimumTests { get; set; } = DefaultElectronicMinimum;

    public static CrossmatchSettings CreateDefault() => new()
    {
        NegativeAntibodyHistoryTestCode = DefaultNegativeTestCode,
        PositiveAntibodyHistoryTestCode = DefaultPositiveTestCode,
        ElectronicCrossmatchTestCode = DefaultElectronicTestCode,
        ElectronicXmMinimumVisits = DefaultElectronicMinimum,
        ElectronicXmMinimumSpecimens = DefaultElectronicMinimum,
        ElectronicXmMinimumTests = DefaultElectronicMinimum,
        Version = 1,
        IsActive = true,
        IsDraft = false
    };
}
