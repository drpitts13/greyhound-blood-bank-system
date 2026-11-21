namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-055 (decided 2026-11-04): an override authorizer who is not in the provider or user directory is not refused,
/// and can be added to the directory as an outside provider. An outside provider is an <c>OrderingProvider</c> row
/// whose source is <see cref="SourceSystem"/>; it is offered as an authorizer but not as an ordering provider.
/// </summary>
public static class OutsideProviderRule
{
    public const string SourceSystem = "Outside";

    public const string PermissionCode = "PROVIDER-OUTSIDE-PERM";
    public const string NameCode = "PROVIDER.OUTSIDE.NAME";
    public const string ReasonCode = "PROVIDER.OUTSIDE.REASON";
    public const string AlreadyListedCode = "PROVIDER.OUTSIDE.LISTED";

    public const int MinimumReasonLength = 8;

    /// <summary>The id given to an outside provider added without one (for example OUT-0003).</summary>
    public static string GeneratedId(int sequence) => $"OUT-{sequence:0000}";

    public static bool IsOutside(string? sourceSystem) =>
        string.Equals(sourceSystem?.Trim(), SourceSystem, StringComparison.OrdinalIgnoreCase);

    public static RuleResult EvaluatePermission(bool hasPermission) =>
        hasPermission
            ? RuleResult.Pass(PermissionCode)
            : RuleResult.HardStop(PermissionCode,
                "Adding an outside provider requires the provider.outside.add permission. The override can still be recorded with the name as typed.");

    /// <summary>
    /// Name and reason are required. A name that already matches someone in the directory is refused, so the same
    /// person is not added twice: pick them from the list instead.
    /// </summary>
    public static RuleEvaluation Validate(string? name, string? reason, bool alreadyInDirectory)
    {
        var results = new List<RuleResult>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2)
        {
            results.Add(RuleResult.HardStop(NameCode, "Enter the outside provider's full name."));
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < MinimumReasonLength)
        {
            results.Add(RuleResult.HardStop(ReasonCode,
                $"Say why this provider is being added (at least {MinimumReasonLength} characters), for example who they are and where they practise."));
        }

        if (alreadyInDirectory)
        {
            results.Add(RuleResult.HardStop(AlreadyListedCode,
                "This name is already in the directory. Pick it from the list instead of adding it again."));
        }

        return new RuleEvaluation(results);
    }
}
