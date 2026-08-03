using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// A configurable clinical rule: a condition over patient/order/test attributes plus
/// the actions taken when it matches. Order-level rules are evaluated when an order is
/// created or updated; test-level rules when a result is verified.
/// Clinically significant: edits are versioned and audited, and activation is gated on
/// the condition and action expressions parsing cleanly (see <c>RuleDefinitionValidator</c>).
/// </summary>
public class RuleDefinition : VersionedConfigEntity
{
    /// <summary>Unique code among active rules.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public RuleLevel Level { get; set; } = RuleLevel.Order;

    /// <summary>Evaluation order within a level, ascending. Ties break on code.</summary>
    public int Priority { get; set; }

    /// <summary>When true, a match stops evaluation of lower-priority rules at the same level.</summary>
    public bool StopOnMatch { get; set; }

    /// <summary>
    /// Condition expression, e.g. <c>patient.ageDays &lt; 1 AND order.hasTest('TS')</c>.
    /// Valid attribute paths are defined by <c>RuleAttributeCatalog</c>.
    /// </summary>
    public string ConditionExpression { get; set; } = string.Empty;

    /// <summary>
    /// Semicolon-separated actions, e.g. <c>cancelTest('TS'); addTest('TSNEO')</c>.
    /// </summary>
    public string ActionExpression { get; set; } = string.Empty;
}
