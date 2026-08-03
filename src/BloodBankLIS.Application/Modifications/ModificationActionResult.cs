using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Modifications;

/// <summary>
/// Outcome of a product-modification operation. Mirrors
/// <see cref="Inventory.InventoryActionResult"/>: distinguishes success (with the
/// header record and the produced result unit(s)), a rule block, and a plain
/// validation failure.
/// </summary>
public sealed class ModificationActionResult
{
    private ModificationActionResult(
        bool succeeded, UnitModification? modification, IReadOnlyList<BloodUnit>? resultUnits, RuleEvaluation? evaluation, string? error)
    {
        Succeeded = succeeded;
        Modification = modification;
        ResultUnits = resultUnits;
        Evaluation = evaluation;
        Error = error;
    }

    public bool Succeeded { get; }

    public UnitModification? Modification { get; }

    public IReadOnlyList<BloodUnit>? ResultUnits { get; }

    public RuleEvaluation? Evaluation { get; }

    public string? Error { get; }

    public static ModificationActionResult Ok(UnitModification modification, IReadOnlyList<BloodUnit> resultUnits) =>
        new(true, modification, resultUnits, null, null);

    public static ModificationActionResult Blocked(RuleEvaluation evaluation) => new(false, null, null, evaluation, null);

    public static ModificationActionResult Fail(string error) => new(false, null, null, null, error);
}
