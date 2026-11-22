using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

/// <summary>
/// Whether patient resulting is blocked right now, by the same rule Save applies (<see cref="ReagentQcResultGate"/>),
/// and the open QC work that blocks it, so the bench sees the stop before keying results.
/// </summary>
public sealed record ReagentQcResultBlock(string Message, IReadOnlyList<ReagentQcWorkItemDto> Work)
{
    public static ReagentQcResultBlock? For(bool policyOn, IReadOnlyList<ReagentQcWorkItemDto> openWork)
    {
        var gate = ReagentQcResultGate.Evaluate(policyOn, openWork.Count > 0);
        return gate.Severity == RuleSeverity.HardStop ? new ReagentQcResultBlock(gate.Message, openWork) : null;
    }
}
