namespace BloodBankLIS.Web.Services;

/// <summary>A single rule message (code + human-readable text) surfaced from the API.</summary>
public sealed record RuleMessage(string Code, string Message);

/// <summary>
/// Non-generic, render-ready summary of an API outcome for the shared Outcome component.
/// </summary>
public sealed record Feedback(
    bool Success,
    string? Message,
    IReadOnlyList<RuleMessage> Warnings,
    bool Blocked,
    bool Overridable,
    IReadOnlyList<RuleMessage> HardStops,
    string? Error)
{
    public static Feedback Fail(string error) =>
        new(false, null, Array.Empty<RuleMessage>(), false, false, Array.Empty<RuleMessage>(), error);

    public static Feedback Succeed(string message) =>
        new(true, message, Array.Empty<RuleMessage>(), false, false, Array.Empty<RuleMessage>(), null);
}

/// <summary>
/// Normalized outcome of an API call. Mirrors the API's response conventions:
/// success may carry non-blocking warnings; a safety gate returns a block with
/// HardStops/Warnings and an <see cref="Overridable"/> flag; other failures carry a
/// message and HTTP status. The UI renders each case consistently.
/// </summary>
public sealed class ApiResult<T>
{
    public bool Succeeded { get; init; }

    public T? Value { get; init; }

    public IReadOnlyList<RuleMessage> Warnings { get; init; } = Array.Empty<RuleMessage>();

    public bool Blocked { get; init; }

    public bool Overridable { get; init; }

    public IReadOnlyList<RuleMessage> HardStops { get; init; } = Array.Empty<RuleMessage>();

    public string? Error { get; init; }

    public int StatusCode { get; init; }

    public bool HasWarnings => Warnings.Count > 0;

    public static ApiResult<T> Ok(T? value, IReadOnlyList<RuleMessage>? warnings = null) => new()
    {
        Succeeded = true,
        Value = value,
        Warnings = warnings ?? Array.Empty<RuleMessage>()
    };

    public static ApiResult<T> Fail(string error, int statusCode) => new()
    {
        Succeeded = false,
        Error = error,
        StatusCode = statusCode
    };

    public static ApiResult<T> Gate(bool overridable, IReadOnlyList<RuleMessage> hardStops, IReadOnlyList<RuleMessage> warnings) => new()
    {
        Succeeded = false,
        Blocked = true,
        Overridable = overridable,
        HardStops = hardStops,
        Warnings = warnings,
        StatusCode = 422
    };

    /// <summary>Build a render-ready feedback model from this result.</summary>
    public Feedback ToFeedback(string? successMessage = null) => new(
        Success: Succeeded,
        Message: Succeeded ? successMessage : null,
        Warnings: Warnings,
        Blocked: Blocked,
        Overridable: Overridable,
        HardStops: HardStops,
        Error: Succeeded || Blocked ? null : FailureSummary());

    /// <summary>A short, user-facing summary of why the call did not succeed.</summary>
    public string FailureSummary()
    {
        if (Succeeded)
        {
            return string.Empty;
        }

        if (Blocked)
        {
            var stops = HardStops.Select(h => h.Message);
            var warns = Warnings.Select(w => w.Message);
            var lines = HardStops.Count > 0 ? stops : warns;
            var prefix = HardStops.Count > 0 ? "Blocked" : "Override required";
            return $"{prefix}: {string.Join("; ", lines)}";
        }

        return StatusCode switch
        {
            401 => "You are not signed in.",
            403 => Error ?? "You do not have permission to perform this action.",
            _ => Error ?? $"Request failed ({StatusCode})."
        };
    }
}
