using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// ABO/Rh(D) compatibility via derived antigen/antibody conflict detection.
/// Type A is assumed to express A antigen and anti-B; whenever either side has an
/// antigen, the other side must not carry the corresponding antibody (and symmetrically).
/// Pure and deterministic; exhaustively unit-tested.
/// </summary>
public static class AboCompatibilityRule
{
    public const string AboCode = "ISS-ABO-COMPAT";
    public const string RhCode = "ISS-RH-COMPAT";
    public const string UnknownTypeCode = "ISS-ABORH-KNOWN";

    /// <summary>A mixed-ABO pool was checked as a single group, or its components are not recorded (fails closed).</summary>
    public const string MixedPoolCode = "ISS-ABO-MIXED-POOL";

    private static RuleResult MixedWithoutComponents() => RuleResult.HardStop(MixedPoolCode,
        "This is a mixed-ABO pool; it is checked against each unit in it, and its pool components are not recorded.");

    private const string AntigenA = "A";
    private const string AntigenB = "B";
    private const string AntigenD = "D";

    /// <summary>True when Rh(D) is a strict (HardStop) constraint for the component class.</summary>
    private static bool RhIsStrict(ComponentClass componentClass) =>
        componentClass is ComponentClass.RedBloodCells or ComponentClass.WholeBlood;

    /// <summary>Component classes whose ABO direction is plasma (unit Abs vs patient Ags).</summary>
    private static bool UsesPlasmaDirection(ComponentClass componentClass) =>
        componentClass is ComponentClass.Plasma
            or ComponentClass.Cryoprecipitate
            or ComponentClass.Platelets;

    /// <summary>Whole blood carries both red cells and plasma — check both directions.</summary>
    private static bool UsesBidirectionalAbo(ComponentClass componentClass) =>
        componentClass is ComponentClass.WholeBlood;

    public static IReadOnlyList<RuleResult> Evaluate(AboRh recipient, AboRh donor, ComponentClass componentClass)
    {
        var results = new List<RuleResult>();

        // Mixed-ABO pooling phase 2: a pool's "Mixed" is never compared as a group (it would carry no antigens and pass
        // anyone); callers pass its components through EvaluateUnit.
        if (recipient.Abo == AboGroup.Mixed || donor.Abo == AboGroup.Mixed)
        {
            return [MixedWithoutComponents()];
        }

        if (recipient.Abo == AboGroup.Unknown || donor.Abo == AboGroup.Unknown)
        {
            results.Add(RuleResult.HardStop(UnknownTypeCode, "ABO group is unknown for recipient or donor."));
            return results;
        }

        results.Add(EvaluateAbo(recipient.Abo, donor.Abo, componentClass));
        results.Add(EvaluateRh(recipient.Rh, donor.Rh, componentClass));
        return results;
    }

    /// <summary>
    /// Evaluates a product ABO/Rh table when one is configured. An empty policy uses the
    /// built-in component-class rule. A configured policy is fail-closed for a missing cell.
    /// </summary>
    public static IReadOnlyList<RuleResult> Evaluate(
        AboRh recipient,
        AboRh donor,
        ComponentClass componentClass,
        Sex patientSex,
        ProductAboRhPolicy? policy)
    {
        if (policy is null || !policy.IsConfigured)
        {
            return Evaluate(recipient, donor, componentClass);
        }

        if (recipient.Abo == AboGroup.Mixed || donor.Abo == AboGroup.Mixed)
        {
            return [MixedWithoutComponents()];
        }

        if (recipient.Abo == AboGroup.Unknown || donor.Abo == AboGroup.Unknown)
        {
            return [RuleResult.HardStop(UnknownTypeCode, "ABO group is unknown for recipient or donor.")];
        }

        var results = new List<RuleResult> { EvaluatePolicyAbo(recipient.Abo, donor.Abo, componentClass, policy) };
        results.Add(EvaluatePolicyRh(recipient.Rh, donor.Rh, patientSex, policy));
        return results;
    }

    private static RuleResult EvaluatePolicyAbo(
        AboGroup recipient,
        AboGroup donor,
        ComponentClass componentClass,
        ProductAboRhPolicy policy)
    {
        if (!policy.TryAbo(recipient, donor, out var allowed))
        {
            return RuleResult.HardStop(
                AboCode,
                $"No ABO compatibility entry is configured for recipient {recipient} and donor {donor}.");
        }

        return allowed
            ? RuleResult.Pass(AboCode)
            : RuleResult.HardStop(
                AboCode,
                $"Donor ABO {donor} is not compatible with recipient ABO {recipient} for {componentClass} (antigen/antibody conflict).");
    }

    private static RuleResult EvaluatePolicyRh(RhType recipient, RhType donor, Sex patientSex, ProductAboRhPolicy policy)
    {
        if (recipient == RhType.Unknown || donor == RhType.Unknown)
        {
            return policy.RhIsConstrained
                ? RuleResult.HardStop(RhCode, "Rh(D) is unknown for recipient or donor.")
                : RuleResult.Pass(RhCode, "Rh(D) is not a strict constraint for this component class.");
        }

        var sex = ProductAboRhPolicy.SexGroup(patientSex);
        if (!policy.TryRh(sex, recipient, donor, out var allowed))
        {
            return RuleResult.HardStop(
                RhCode,
                $"No Rh compatibility entry is configured for {sex} recipients with Rh {recipient} and donor Rh {donor}.");
        }

        if (!allowed)
        {
            return recipient == RhType.Negative && donor == RhType.Positive
                ? RuleResult.HardStop(
                    RhCode,
                    "Rh-positive red cells cannot be given to an Rh-negative recipient outside emergency policy.")
                : RuleResult.HardStop(
                    RhCode,
                    $"Donor Rh {donor} is not compatible with recipient Rh {recipient} for {sex} patients.");
        }

        return RuleResult.Pass(RhCode);
    }

    private static RuleResult EvaluateAbo(AboGroup recipient, AboGroup donor, ComponentClass componentClass)
    {
        var recipientProfile = DeriveAboProfile(recipient);
        var donorProfile = DeriveAboProfile(donor);

        bool conflict;
        if (UsesBidirectionalAbo(componentClass))
        {
            // Cellular: patient Abs vs unit Ags; plasma: unit Abs vs patient Ags.
            conflict = HasAntigenAntibodyConflict(recipientProfile.Antibodies, donorProfile.Antigens)
                || HasAntigenAntibodyConflict(donorProfile.Antibodies, recipientProfile.Antigens);
        }
        else if (UsesPlasmaDirection(componentClass))
        {
            conflict = HasAntigenAntibodyConflict(donorProfile.Antibodies, recipientProfile.Antigens);
        }
        else
        {
            // RBC, granulocytes, Other: patient Abs vs unit Ags.
            conflict = HasAntigenAntibodyConflict(recipientProfile.Antibodies, donorProfile.Antigens);
        }

        return conflict
            ? RuleResult.HardStop(
                AboCode,
                $"Donor ABO {donor} is not compatible with recipient ABO {recipient} for {componentClass} (antigen/antibody conflict).")
            : RuleResult.Pass(AboCode);
    }

    private static RuleResult EvaluateRh(RhType recipient, RhType donor, ComponentClass componentClass)
    {
        // Rh(D) only constrains red-cell-bearing components. Rh-negative
        // recipients must not receive Rh-positive red cells (HardStop).
        // Anti-D is not assumed from Rh-negative typing alone.
        if (!RhIsStrict(componentClass))
        {
            return RuleResult.Pass(RhCode, "Rh(D) is not a strict constraint for this component class.");
        }

        if (recipient == RhType.Unknown || donor == RhType.Unknown)
        {
            return RuleResult.HardStop(RhCode, "Rh(D) is unknown for recipient or donor.");
        }

        // Rh+ donor expresses D antigen; Rh- recipient must not receive D antigen on RBC/WB.
        if (recipient == RhType.Negative && donor == RhType.Positive)
        {
            return RuleResult.HardStop(
                RhCode,
                "Rh-positive red cells cannot be given to an Rh-negative recipient outside emergency policy.");
        }

        return RuleResult.Pass(RhCode);
    }

    /// <summary>
    /// True when any antibody on one side matches an antigen present on the other.
    /// </summary>
    internal static bool HasAntigenAntibodyConflict(
        IReadOnlyCollection<string> antibodies,
        IReadOnlyCollection<string> antigens)
    {
        if (antibodies.Count == 0 || antigens.Count == 0)
        {
            return false;
        }

        return antibodies.Any(ab => antigens.Contains(ab, StringComparer.Ordinal));
    }

    /// <summary>
    /// Mixed-ABO pooling phase 2 (docs/architecture/mixed-abo-pooling.md): checks a unit against the recipient. A pool
    /// with recorded components is compatible only if every distinct component ABO/Rh is, under the same rule or product
    /// table; any other unit is checked as itself. A unit whose ABO is Mixed but whose components are missing is refused.
    /// </summary>
    public static IReadOnlyList<RuleResult> EvaluateUnit(
        AboRh recipient,
        AboRh unit,
        IReadOnlyList<PoolComponent>? components,
        ComponentClass componentClass,
        Sex patientSex = Sex.Unknown,
        ProductAboRhPolicy? policy = null)
    {
        if (components is not { Count: > 0 })
        {
            return Evaluate(recipient, unit, componentClass, patientSex, policy);
        }

        var results = new List<RuleResult>();
        foreach (var part in components.Distinct())
        {
            var partResults = Evaluate(recipient, new AboRh(part.Abo, part.RhD), componentClass, patientSex, policy);
            results.AddRange(partResults.Where(r => r.Severity != RuleSeverity.Pass).Select(r =>
                new RuleResult(r.Code, r.Severity, $"In this pool, the {Group(part)} unit: {r.Message}")));
        }

        return results.Count > 0 ? results : [RuleResult.Pass(AboCode), RuleResult.Pass(RhCode)];
    }

    private static string Group(PoolComponent part) =>
        $"{part.Abo} {(part.RhD == RhType.Positive ? "Rh positive" : part.RhD == RhType.Negative ? "Rh negative" : "Rh unknown")}";

    public static AboProfile DeriveAboProfile(AboGroup abo) => abo switch
    {
        AboGroup.O => new AboProfile([], [AntigenA, AntigenB]),
        AboGroup.A => new AboProfile([AntigenA], [AntigenB]),
        AboGroup.B => new AboProfile([AntigenB], [AntigenA]),
        AboGroup.AB => new AboProfile([AntigenA, AntigenB], []),
        _ => new AboProfile([], [])
    };

    public readonly record struct AboProfile(
        IReadOnlyList<string> Antigens,
        IReadOnlyList<string> Antibodies);
}
