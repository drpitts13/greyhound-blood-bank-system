using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Resolves result provenance. Catalog-logic / reaction-panel interpretations are
/// Calculated unless the observation already arrived as Instrument or Interface.
/// </summary>
public static class ResultProvenanceRule
{
    public static ResultSource Resolve(ResultSource requested, bool catalogLogicApplied)
    {
        if (!catalogLogicApplied)
        {
            return requested;
        }

        if (requested is ResultSource.Instrument or ResultSource.Interface)
        {
            return requested;
        }

        return ResultSource.Calculated;
    }
}
