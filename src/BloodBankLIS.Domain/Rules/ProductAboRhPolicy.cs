using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Allow/deny ABO and Rh table for one product. An empty policy means the built-in
/// component-class rule still applies. A saved policy is fail-closed: a missing cell
/// is not allowed.
/// </summary>
public sealed class ProductAboRhPolicy
{
    private readonly Dictionary<(AboGroup Patient, AboGroup Unit), bool> _abo;
    private readonly Dictionary<(RhCompatibilitySex Sex, RhType Patient, RhType Unit), bool> _rh;

    public ProductAboRhPolicy(IEnumerable<AboCell> aboCells, IEnumerable<RhCell> rhCells)
    {
        AboCells = aboCells.ToList();
        RhCells = rhCells.ToList();
        _abo = new Dictionary<(AboGroup, AboGroup), bool>();
        foreach (var cell in AboCells)
        {
            _abo[(cell.PatientAbo, cell.UnitAbo)] = cell.IsAllowed;
        }

        _rh = new Dictionary<(RhCompatibilitySex, RhType, RhType), bool>();
        foreach (var cell in RhCells)
        {
            _rh[(cell.SexGroup, cell.PatientRh, cell.UnitRh)] = cell.IsAllowed;
        }
    }

    public IReadOnlyList<AboCell> AboCells { get; }

    public IReadOnlyList<RhCell> RhCells { get; }

    public bool IsConfigured => AboCells.Count > 0 || RhCells.Count > 0;

    /// <summary>True when any Rh cell is denied, so unknown Rh cannot pass.</summary>
    public bool RhIsConstrained => _rh.Values.Any(allowed => !allowed);

    public bool IsComplete
    {
        get
        {
            if (AboCells.Count != StandardProductAboRhPolicy.AboCellCount
                || RhCells.Count != StandardProductAboRhPolicy.RhCellCount)
            {
                return false;
            }

            var aboKeys = AboCells.Select(c => (c.PatientAbo, c.UnitAbo)).ToHashSet();
            if (aboKeys.Count != StandardProductAboRhPolicy.AboCellCount)
            {
                return false;
            }

            foreach (var patient in StandardProductAboRhPolicy.AboGroups)
            {
                foreach (var unit in StandardProductAboRhPolicy.AboGroups)
                {
                    if (!aboKeys.Contains((patient, unit)))
                    {
                        return false;
                    }
                }
            }

            var rhKeys = RhCells.Select(c => (c.SexGroup, c.PatientRh, c.UnitRh)).ToHashSet();
            if (rhKeys.Count != StandardProductAboRhPolicy.RhCellCount)
            {
                return false;
            }

            foreach (var sex in StandardProductAboRhPolicy.SexGroups)
            {
                foreach (var patient in StandardProductAboRhPolicy.RhTypes)
                {
                    foreach (var unit in StandardProductAboRhPolicy.RhTypes)
                    {
                        if (!rhKeys.Contains((sex, patient, unit)))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }

    public bool TryAbo(AboGroup patient, AboGroup unit, out bool allowed) =>
        _abo.TryGetValue((patient, unit), out allowed);

    public bool TryRh(RhCompatibilitySex sex, RhType patient, RhType unit, out bool allowed) =>
        _rh.TryGetValue((sex, patient, unit), out allowed);

    public ProductAboRhPolicy WithRh(RhCompatibilitySex sex, RhType patientRh, RhType unitRh, bool isAllowed)
    {
        var replaced = false;
        var cells = new List<RhCell>(RhCells.Count);
        foreach (var cell in RhCells)
        {
            if (cell.SexGroup == sex && cell.PatientRh == patientRh && cell.UnitRh == unitRh)
            {
                cells.Add(cell with { IsAllowed = isAllowed });
                replaced = true;
            }
            else
            {
                cells.Add(cell);
            }
        }

        if (!replaced)
        {
            cells.Add(new RhCell(sex, patientRh, unitRh, isAllowed));
        }

        return new ProductAboRhPolicy(AboCells, cells);
    }

    public static RhCompatibilitySex SexGroup(Sex sex) => sex switch
    {
        Sex.Male => RhCompatibilitySex.Male,
        Sex.Female => RhCompatibilitySex.Female,
        _ => RhCompatibilitySex.UnknownOrOther
    };

    public readonly record struct AboCell(AboGroup PatientAbo, AboGroup UnitAbo, bool IsAllowed);

    public readonly record struct RhCell(RhCompatibilitySex SexGroup, RhType PatientRh, RhType UnitRh, bool IsAllowed);
}

/// <summary>
/// Full ABO and Rh grids that reproduce <see cref="AboCompatibilityRule"/> for a component class.
/// </summary>
public static class StandardProductAboRhPolicy
{
    public static readonly AboGroup[] AboGroups = [AboGroup.O, AboGroup.A, AboGroup.B, AboGroup.AB];

    public static readonly RhType[] RhTypes = [RhType.Negative, RhType.Positive];

    public static readonly RhCompatibilitySex[] SexGroups =
    [
        RhCompatibilitySex.Male,
        RhCompatibilitySex.Female,
        RhCompatibilitySex.UnknownOrOther
    ];

    public const int AboCellCount = 16;

    public const int RhCellCount = 12;

    public static ProductAboRhPolicy For(ComponentClass componentClass)
    {
        var abo = new List<ProductAboRhPolicy.AboCell>(AboCellCount);
        foreach (var patient in AboGroups)
        {
            foreach (var unit in AboGroups)
            {
                var results = AboCompatibilityRule.Evaluate(
                    new AboRh(patient, RhType.Negative),
                    new AboRh(unit, RhType.Negative),
                    componentClass);
                var allowed = results.Single(r => r.Code == AboCompatibilityRule.AboCode).Severity == RuleSeverity.Pass;
                abo.Add(new ProductAboRhPolicy.AboCell(patient, unit, allowed));
            }
        }

        var rh = new List<ProductAboRhPolicy.RhCell>(RhCellCount);
        foreach (var sex in SexGroups)
        {
            foreach (var patient in RhTypes)
            {
                foreach (var unit in RhTypes)
                {
                    var results = AboCompatibilityRule.Evaluate(
                        new AboRh(AboGroup.O, patient),
                        new AboRh(AboGroup.O, unit),
                        componentClass);
                    var allowed = results.Single(r => r.Code == AboCompatibilityRule.RhCode).Severity == RuleSeverity.Pass;
                    rh.Add(new ProductAboRhPolicy.RhCell(sex, patient, unit, allowed));
                }
            }
        }

        return new ProductAboRhPolicy(abo, rh);
    }
}
