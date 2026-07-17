using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Data-driven ABO/Rh(D) compatibility, evaluated by component class. RBC and
/// plasma run in opposite directions (see docs/safety-rules.md section 3). Pure and
/// deterministic; the matrices are exhaustively unit-tested.
/// </summary>
public static class AboCompatibilityRule
{
    public const string AboCode = "ISS-ABO-COMPAT";
    public const string RhCode = "ISS-RH-COMPAT";
    public const string UnknownTypeCode = "ISS-ABORH-KNOWN";

    // RBC: recipient ABO -> donor ABO groups that may be transfused.
    private static readonly IReadOnlyDictionary<AboGroup, AboGroup[]> RbcAboCompatibility =
        new Dictionary<AboGroup, AboGroup[]>
        {
            [AboGroup.O] = new[] { AboGroup.O },
            [AboGroup.A] = new[] { AboGroup.A, AboGroup.O },
            [AboGroup.B] = new[] { AboGroup.B, AboGroup.O },
            [AboGroup.AB] = new[] { AboGroup.AB, AboGroup.A, AboGroup.B, AboGroup.O }
        };

    // Plasma: inverse direction. Recipient ABO -> donor plasma ABO groups allowed.
    private static readonly IReadOnlyDictionary<AboGroup, AboGroup[]> PlasmaAboCompatibility =
        new Dictionary<AboGroup, AboGroup[]>
        {
            [AboGroup.AB] = new[] { AboGroup.AB },
            [AboGroup.A] = new[] { AboGroup.A, AboGroup.AB },
            [AboGroup.B] = new[] { AboGroup.B, AboGroup.AB },
            [AboGroup.O] = new[] { AboGroup.O, AboGroup.A, AboGroup.B, AboGroup.AB }
        };

    /// <summary>True when Rh(D) is a strict (HardStop) constraint for the component class.</summary>
    private static bool RhIsStrict(ComponentClass componentClass) =>
        componentClass is ComponentClass.RedBloodCells or ComponentClass.WholeBlood;

    /// <summary>Component classes whose ABO direction is the plasma (inverse) matrix.</summary>
    private static bool UsesPlasmaDirection(ComponentClass componentClass) =>
        componentClass is ComponentClass.Plasma or ComponentClass.Cryoprecipitate;

    public static IReadOnlyList<RuleResult> Evaluate(AboRh recipient, AboRh donor, ComponentClass componentClass)
    {
        var results = new List<RuleResult>();

        if (recipient.Abo == AboGroup.Unknown || donor.Abo == AboGroup.Unknown)
        {
            results.Add(RuleResult.HardStop(UnknownTypeCode, "ABO group is unknown for recipient or donor."));
            return results;
        }

        var matrix = UsesPlasmaDirection(componentClass) ? PlasmaAboCompatibility : RbcAboCompatibility;
        var aboCompatible = matrix.TryGetValue(recipient.Abo, out var allowed) && allowed.Contains(donor.Abo);

        results.Add(aboCompatible
            ? RuleResult.Pass(AboCode)
            : RuleResult.HardStop(AboCode, $"Donor ABO {donor.Abo} is not compatible with recipient ABO {recipient.Abo} for {componentClass}."));

        results.Add(EvaluateRh(recipient.Rh, donor.Rh, componentClass));
        return results;
    }

    private static RuleResult EvaluateRh(RhType recipient, RhType donor, ComponentClass componentClass)
    {
        // Rh(D) only constrains red-cell-bearing components here. Rh-negative
        // recipients must not receive Rh-positive red cells (HardStop).
        if (!RhIsStrict(componentClass))
        {
            return RuleResult.Pass(RhCode, "Rh(D) is not a strict constraint for this component class.");
        }

        if (recipient == RhType.Unknown || donor == RhType.Unknown)
        {
            return RuleResult.HardStop(RhCode, "Rh(D) is unknown for recipient or donor.");
        }

        return recipient == RhType.Negative && donor == RhType.Positive
            ? RuleResult.HardStop(RhCode, "Rh-positive red cells cannot be given to an Rh-negative recipient outside emergency policy.")
            : RuleResult.Pass(RhCode);
    }
}
