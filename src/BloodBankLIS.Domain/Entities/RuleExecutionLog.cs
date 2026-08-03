using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Append-only record of one <c>RuleDefinition</c> firing against one context.
/// Serves two purposes: it is the idempotency key (a rule fires at most once per
/// order or per verified result), and it explains after the fact why a test appeared
/// on or disappeared from an order.
/// </summary>
public class RuleExecutionLog : BaseEntity
{
    public long RuleId { get; set; }

    /// <summary>Denormalized so history survives the rule being renamed or retired.</summary>
    public string RuleCode { get; set; } = string.Empty;

    public int RuleVersion { get; set; }

    public RuleLevel Level { get; set; }

    public long PatientId { get; set; }

    public long? OrderId { get; set; }

    public long? TestResultId { get; set; }

    /// <summary>JSON array of the action calls that were applied.</summary>
    public string? ActionsJson { get; set; }

    /// <summary>Warnings raised while applying actions, e.g. a test that could not be cancelled.</summary>
    public string? Notes { get; set; }

    public DateTime EvaluatedUtc { get; set; }
}
