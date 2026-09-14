using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Maps open workup status to the next bench action. Advisory only —
/// does not identify antibodies or complete a workup.
/// </summary>
public static class AntibodyIdentificationWorklistRule
{
    public const string UnusableSpecimenFilter = "unusable";
    public const string ExpiredSpecimenFilter = "expired";
    public const string UnacceptedSpecimenFilter = "unaccepted";
    public const string NotReadySpecimenFilter = "notready";
    public const string RecordReactionsFilter = "reactions";
    public const string InterpretFilter = "interpret";
    public const string ReviewFilter = "review";
    public const string LotAttentionFilter = "lotAttention";
    public const string WithdrawnJudgmentFilter = "withdrawn";
    public const string PendingTypeCorrectionFilter = "typePending";
    public const string OpenProductsFilter = "products";

    public static AntibodyIdWorklistNextAction NextAction(AntibodyWorkupStatus status) =>
        status switch
        {
            AntibodyWorkupStatus.PendingInterpretation => AntibodyIdWorklistNextAction.Interpret,
            AntibodyWorkupStatus.PendingSupervisorReview => AntibodyIdWorklistNextAction.Review,
            AntibodyWorkupStatus.InProgress => AntibodyIdWorklistNextAction.RecordReactions,
            _ => AntibodyIdWorklistNextAction.None
        };

    /// <summary>
    /// Dashboard and worklist query filters for specimen attention.
    /// Advisory only — does not identify antibodies.
    /// </summary>
    public static bool MatchesSpecimenAttention(
        bool hasUnusableSpecimen,
        bool hasExpiredSpecimen,
        string? specimenFilter,
        bool hasUnacceptedSpecimen = false,
        bool hasNotReadySpecimen = false)
    {
        if (string.IsNullOrWhiteSpace(specimenFilter))
        {
            return true;
        }

        if (string.Equals(specimenFilter, UnusableSpecimenFilter, StringComparison.OrdinalIgnoreCase))
        {
            return hasUnusableSpecimen;
        }

        if (string.Equals(specimenFilter, ExpiredSpecimenFilter, StringComparison.OrdinalIgnoreCase))
        {
            return hasExpiredSpecimen;
        }

        if (string.Equals(specimenFilter, UnacceptedSpecimenFilter, StringComparison.OrdinalIgnoreCase))
        {
            return hasUnacceptedSpecimen;
        }

        if (string.Equals(specimenFilter, NotReadySpecimenFilter, StringComparison.OrdinalIgnoreCase))
        {
            return hasNotReadySpecimen;
        }

        return true;
    }

    /// <summary>
    /// Dashboard and worklist query filters for next bench action.
    /// Advisory only — does not identify antibodies.
    /// </summary>
    public static bool MatchesNextAction(
        AntibodyIdWorklistNextAction nextAction,
        string? actionFilter)
    {
        if (string.IsNullOrWhiteSpace(actionFilter))
        {
            return true;
        }

        if (string.Equals(actionFilter, RecordReactionsFilter, StringComparison.OrdinalIgnoreCase))
        {
            return nextAction == AntibodyIdWorklistNextAction.RecordReactions;
        }

        if (string.Equals(actionFilter, InterpretFilter, StringComparison.OrdinalIgnoreCase))
        {
            return nextAction == AntibodyIdWorklistNextAction.Interpret;
        }

        if (string.Equals(actionFilter, ReviewFilter, StringComparison.OrdinalIgnoreCase))
        {
            return nextAction == AntibodyIdWorklistNextAction.Review;
        }

        return true;
    }

    /// <summary>
    /// Dashboard and worklist query filters for an inactive or expired
    /// attached lot. Advisory only — does not identify antibodies.
    /// </summary>
    public static bool MatchesLotAttention(
        bool hasInactiveLot,
        bool hasExpiredLot,
        string? lotAttentionFilter)
    {
        if (string.IsNullOrWhiteSpace(lotAttentionFilter))
        {
            return true;
        }

        if (string.Equals(lotAttentionFilter, LotAttentionFilter, StringComparison.OrdinalIgnoreCase))
        {
            return hasInactiveLot || hasExpiredLot;
        }

        return true;
    }

    /// <summary>
    /// Dashboard and worklist query filters for withdrawn interpretation.
    /// Advisory only — does not identify antibodies.
    /// </summary>
    public static bool MatchesWithdrawnJudgment(
        bool hasWithdrawnJudgment,
        string? withdrawnFilter)
    {
        if (string.IsNullOrWhiteSpace(withdrawnFilter))
        {
            return true;
        }

        if (string.Equals(withdrawnFilter, WithdrawnJudgmentFilter, StringComparison.OrdinalIgnoreCase))
        {
            return hasWithdrawnJudgment;
        }

        return true;
    }

    /// <summary>
    /// Dashboard and worklist query filters for a pending ABO/Rh or antigen
    /// correction that HardStops complete. Advisory only — does not identify
    /// antibodies or verify the correction.
    /// </summary>
    public static bool MatchesPendingTypeCorrection(
        bool hasPendingTypeCorrection,
        string? pendingTypeFilter)
    {
        if (string.IsNullOrWhiteSpace(pendingTypeFilter))
        {
            return true;
        }

        if (string.Equals(pendingTypeFilter, PendingTypeCorrectionFilter, StringComparison.OrdinalIgnoreCase))
        {
            return hasPendingTypeCorrection;
        }

        return true;
    }

    /// <summary>
    /// Dashboard and worklist query filters for reserved or issued units.
    /// Advisory only — does not identify antibodies or release units.
    /// </summary>
    public static bool MatchesOpenProducts(
        bool hasReservedOrIssuedUnits,
        string? productsFilter)
    {
        if (string.IsNullOrWhiteSpace(productsFilter))
        {
            return true;
        }

        if (string.Equals(productsFilter, OpenProductsFilter, StringComparison.OrdinalIgnoreCase))
        {
            return hasReservedOrIssuedUnits;
        }

        return true;
    }
}
