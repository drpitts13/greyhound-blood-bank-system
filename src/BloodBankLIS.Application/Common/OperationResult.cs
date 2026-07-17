using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Common;

/// <summary>
/// Generic result for application operations. Carries the produced value on success,
/// any non-blocking warnings (e.g. an ABO/Rh delta), or a validation/state error.
/// </summary>
public sealed class OperationResult<T>
{
    private OperationResult(bool succeeded, T? value, string? error, IReadOnlyList<RuleResult> warnings)
    {
        Succeeded = succeeded;
        Value = value;
        Error = error;
        Warnings = warnings;
    }

    public bool Succeeded { get; }

    public T? Value { get; }

    public string? Error { get; }

    public IReadOnlyList<RuleResult> Warnings { get; }

    public bool HasWarnings => Warnings.Count > 0;

    public static OperationResult<T> Ok(T value, IReadOnlyList<RuleResult>? warnings = null) =>
        new(true, value, null, warnings ?? Array.Empty<RuleResult>());

    public static OperationResult<T> Fail(string error) =>
        new(false, default, error, Array.Empty<RuleResult>());
}
