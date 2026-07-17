using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

/// <summary>
/// Outcome of an inventory operation. Distinguishes success, a rule block
/// (carrying the <see cref="RuleEvaluation"/> so callers can show which checks
/// fired), and a validation failure (e.g. duplicate unit number, missing reason).
/// </summary>
public sealed class InventoryActionResult
{
    private InventoryActionResult(bool succeeded, BloodUnit? unit, RuleEvaluation? evaluation, string? error)
    {
        Succeeded = succeeded;
        Unit = unit;
        Evaluation = evaluation;
        Error = error;
    }

    public bool Succeeded { get; }

    public BloodUnit? Unit { get; }

    public RuleEvaluation? Evaluation { get; }

    public string? Error { get; }

    public static InventoryActionResult Ok(BloodUnit unit) => new(true, unit, null, null);

    public static InventoryActionResult Blocked(RuleEvaluation evaluation) => new(false, null, evaluation, null);

    public static InventoryActionResult Fail(string error) => new(false, null, null, error);
}
