using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Isbt128.Parsing;

/// <summary>Structured parse result with machine-readable errors.</summary>
public sealed class ParseOutcome<T>
{
    private ParseOutcome(bool success, T? value, IReadOnlyList<RuleResult> errors)
    {
        Success = success;
        Value = value;
        Errors = errors;
    }

    public bool Success { get; }
    public T? Value { get; }
    public IReadOnlyList<RuleResult> Errors { get; }

    public static ParseOutcome<T> Ok(T value) => new(true, value, Array.Empty<RuleResult>());

    public static ParseOutcome<T> Fail(params RuleResult[] errors) =>
        new(false, default, errors);

    public static ParseOutcome<T> Fail(string code, string message) =>
        Fail(RuleResult.HardStop(code, message));
}
