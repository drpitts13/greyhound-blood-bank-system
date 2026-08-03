namespace BloodBankLIS.Domain.Isbt128.Validation;

public sealed class ValidationResult
{
    public bool Valid { get; init; }
    public IReadOnlyList<ValidationMessage> Errors { get; init; } = Array.Empty<ValidationMessage>();
    public IReadOnlyList<ValidationMessage> Warnings { get; init; } = Array.Empty<ValidationMessage>();

    public static ValidationResult Success(params ValidationMessage[] warnings) =>
        new() { Valid = true, Warnings = warnings };

    public static ValidationResult Failure(IEnumerable<ValidationMessage> errors, IEnumerable<ValidationMessage>? warnings = null) =>
        new()
        {
            Valid = false,
            Errors = errors.ToList(),
            Warnings = warnings?.ToList() ?? new List<ValidationMessage>()
        };
}

public sealed class ValidationMessage
{
    public required string Code { get; init; }
    public string? Field { get; init; }
    public required string Message { get; init; }
    public required string Severity { get; init; } // ERROR | WARNING | INFO
    public bool OverrideAllowed { get; init; }
    public string? RequiredRole { get; init; }

    public static ValidationMessage Error(string code, string message, string? field = null, bool overrideAllowed = false, string? requiredRole = null) =>
        new()
        {
            Code = code,
            Message = message,
            Field = field,
            Severity = "ERROR",
            OverrideAllowed = overrideAllowed,
            RequiredRole = requiredRole
        };

    public static ValidationMessage Warning(string code, string message, string? field = null, bool overrideAllowed = true, string? requiredRole = null) =>
        new()
        {
            Code = code,
            Message = message,
            Field = field,
            Severity = "WARNING",
            OverrideAllowed = overrideAllowed,
            RequiredRole = requiredRole
        };
}
