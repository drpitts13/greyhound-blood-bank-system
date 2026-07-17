using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Common;

/// <summary>
/// Result of a safety-gated operation. Distinguishes success, a rule block (carrying
/// the full <see cref="RuleEvaluation"/> so callers can show which checks fired and
/// whether the block is overridable), and a plain validation failure.
/// </summary>
public sealed class EvaluationResult<T>
{
    private EvaluationResult(bool succeeded, T? value, RuleEvaluation? evaluation, string? error)
    {
        Succeeded = succeeded;
        Value = value;
        Evaluation = evaluation;
        Error = error;
    }

    public bool Succeeded { get; }

    public T? Value { get; }

    public RuleEvaluation? Evaluation { get; }

    public string? Error { get; }

    /// <summary>True when blocked only by Warnings, so an authorized override may proceed.</summary>
    public bool RequiresOverride => !Succeeded && Evaluation is { RequiresOverride: true };

    public static EvaluationResult<T> Ok(T value, RuleEvaluation? evaluation = null) =>
        new(true, value, evaluation, null);

    public static EvaluationResult<T> Blocked(RuleEvaluation evaluation) =>
        new(false, default, evaluation, null);

    public static EvaluationResult<T> Fail(string error) =>
        new(false, default, null, error);
}
