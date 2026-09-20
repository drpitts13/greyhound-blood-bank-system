using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules.Engine;

namespace BloodBankLIS.Domain.Instruments;

public sealed record InstrumentRuleFire(string RuleName, string Condition, string Action, IReadOnlyDictionary<string, string> Changes);

/// <summary>Applies ordered instrument interface rules to a mapped field bag.</summary>
public static class InstrumentRuleEngine
{
    public static (Dictionary<string, string?> Fields, IReadOnlyList<InstrumentRuleFire> Trace) Apply(
        IReadOnlyDictionary<string, string?> fields,
        IEnumerable<InstrumentInterfaceRule> rules)
    {
        var working = new Dictionary<string, string?>(fields, StringComparer.OrdinalIgnoreCase);
        var trace = new List<InstrumentRuleFire>();

        foreach (var rule in rules.Where(r => r.IsEnabled).OrderBy(r => r.SortOrder).ThenBy(r => r.Id))
        {
            if (!RuleExpressionParser.TryParse(rule.ConditionExpression, out var node, out _)
                || !InstrumentRuleActions.TryParse(rule.ActionExpression, out var actions, out _))
            {
                continue;
            }

            var facts = InstrumentRuleActions.ToFacts(working);
            if (!RuleExpressionEvaluator.IsSatisfied(node, facts))
            {
                continue;
            }

            var changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var action in actions)
            {
                working[action.Field] = action.Value;
                changes[action.Field] = action.Value;
            }

            trace.Add(new InstrumentRuleFire(rule.Name, rule.ConditionExpression, rule.ActionExpression, changes));
            if (rule.StopOnMatch)
            {
                break;
            }
        }

        return (working, trace);
    }
}
