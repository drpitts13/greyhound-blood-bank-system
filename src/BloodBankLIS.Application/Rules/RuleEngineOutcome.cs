using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Rules;

/// <summary>
/// Result of running the order-level rules over a pending set of order lines.
/// The lines are returned rather than mutated in place so the caller decides when to
/// commit. Execution logs are staged until the order has an identity.
/// </summary>
public sealed class OrderRuleOutcome
{
    public static OrderRuleOutcome Empty { get; } = new(Array.Empty<OrderLine>(), Array.Empty<RuleResult>(), null, Array.Empty<RuleExecutionLog>());

    public OrderRuleOutcome(
        IReadOnlyList<OrderLine> lines,
        IReadOnlyList<RuleResult> warnings,
        string? blockMessage,
        IReadOnlyList<RuleExecutionLog> logs)
    {
        Lines = lines;
        Warnings = warnings;
        BlockMessage = blockMessage;
        Logs = logs;
    }

    /// <summary>The pending order lines after rule actions were applied.</summary>
    public IReadOnlyList<OrderLine> Lines { get; }

    public IReadOnlyList<RuleResult> Warnings { get; }

    /// <summary>Set when a rule invoked <c>block(...)</c>; the order must not be saved.</summary>
    public string? BlockMessage { get; }

    public bool IsBlocked => BlockMessage is not null;

    /// <summary>Execution logs awaiting the order identity. Persisted by the caller after save.</summary>
    public IReadOnlyList<RuleExecutionLog> Logs { get; }
}

/// <summary>Result of running the test-level rules for one verified result.</summary>
public sealed class TestRuleOutcome
{
    public static TestRuleOutcome Empty { get; } = new(Array.Empty<RuleResult>(), Array.Empty<string>());

    public TestRuleOutcome(IReadOnlyList<RuleResult> warnings, IReadOnlyList<string> addedTestCodes)
    {
        Warnings = warnings;
        AddedTestCodes = addedTestCodes;
    }

    public IReadOnlyList<RuleResult> Warnings { get; }

    public IReadOnlyList<string> AddedTestCodes { get; }
}
