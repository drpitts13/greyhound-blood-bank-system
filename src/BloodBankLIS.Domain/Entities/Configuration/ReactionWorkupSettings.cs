using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Singleton, versioned: which Test Group is ordered on each transfusion reaction workup specimen. The pre-transfusion
/// specimen (collected before the unit was issued) and the post-transfusion specimen (drawn after the reaction) each
/// get the tests of their group, resulted on the bench. Edits are audited; the row is never deleted.
/// </summary>
public class ReactionWorkupSettings : VersionedConfigEntity
{
    /// <summary>Starter groups (OCD-059): examples a facility edits, not clinical policy.</summary>
    public const string DefaultPreTransfusionGroupCode = "TRX-PRE";
    public const string DefaultPostTransfusionGroupCode = "TRX-POST";

    /// <summary>Test Group (<see cref="TestGrouper.Code"/>) for the pre-transfusion specimen; null until chosen.</summary>
    public string? PreTransfusionTestGroupCode { get; set; }

    /// <summary>Test Group for the post-transfusion specimen; null until chosen.</summary>
    public string? PostTransfusionTestGroupCode { get; set; }

    public static ReactionWorkupSettings CreateDefault() => new()
    {
        PreTransfusionTestGroupCode = DefaultPreTransfusionGroupCode,
        PostTransfusionTestGroupCode = DefaultPostTransfusionGroupCode,
        Version = 1,
        IsActive = true,
        IsDraft = false
    };
}
