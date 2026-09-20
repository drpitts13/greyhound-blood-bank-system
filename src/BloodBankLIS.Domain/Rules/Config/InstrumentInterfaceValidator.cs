using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;

namespace BloodBankLIS.Domain.Rules.Config;

/// <summary>Pure validation for instrument interface configuration.</summary>
public static class InstrumentInterfaceValidator
{
    public static RuleEvaluation Validate(
        InstrumentInterface entity,
        bool duplicateName,
        bool duplicatePort,
        IReadOnlyList<InstrumentFieldMapping> fieldMappings,
        IReadOnlyList<InstrumentTestMapping> testMappings,
        IReadOnlyList<InstrumentInterfaceRule> rules)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(entity.Name))
        {
            results.Add(RuleResult.HardStop("INST-NAME", "Interface name is required."));
        }

        if (duplicateName)
        {
            results.Add(RuleResult.HardStop("INST-NAME-DUP", "An instrument interface with this name already exists."));
        }

        var connection = entity.Connection;
        if (connection is null || connection.Port is < 1 or > 65535)
        {
            results.Add(RuleResult.HardStop("INST-PORT", "A TCP port between 1 and 65535 is required."));
        }

        if (duplicatePort)
        {
            results.Add(RuleResult.HardStop("INST-PORT-DUP", "Another enabled instrument interface already uses this listen/connect port."));
        }

        if (!string.IsNullOrWhiteSpace(connection?.IpAddress)
            && !System.Net.IPAddress.TryParse(connection.IpAddress.Trim(), out _))
        {
            results.Add(RuleResult.HardStop("INST-IP", "IP address must be a valid IPv4 or IPv6 address, or left blank to listen."));
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var map in fieldMappings)
        {
            if (string.IsNullOrWhiteSpace(map.DataItemKey) || string.IsNullOrWhiteSpace(map.AstmPath))
            {
                results.Add(RuleResult.HardStop("INST-FLD", "Each field mapping needs a data item and an ASTM path."));
                continue;
            }

            if (!InstrumentDataItemKeys.All.Contains(map.DataItemKey, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(RuleResult.HardStop("INST-FLD-KEY", $"Unknown data item '{map.DataItemKey}'."));
            }

            if (!InstrumentAstmPath.TryParse(map.AstmPath, out _))
            {
                results.Add(RuleResult.HardStop("INST-FLD-PATH", $"ASTM path '{map.AstmPath}' is invalid. Use forms like P.3 or R.4.1."));
            }

            if (!keys.Add(map.DataItemKey.Trim()))
            {
                results.Add(RuleResult.HardStop("INST-FLD-DUP", $"Data item '{map.DataItemKey}' is mapped more than once."));
            }
        }

        var outboundApp = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var map in testMappings)
        {
            if (string.IsNullOrWhiteSpace(map.InstrumentCode) || string.IsNullOrWhiteSpace(map.ApplicationCode))
            {
                results.Add(RuleResult.HardStop("INST-MAP", "Each test mapping needs an instrument code and an application code."));
                continue;
            }

            if (map.Direction is InterfaceTranslationDirection.Outbound or InterfaceTranslationDirection.Both)
            {
                var key = $"{map.Kind}:{map.ApplicationCode.Trim()}";
                if (!outboundApp.Add(key))
                {
                    results.Add(RuleResult.HardStop(
                        "INST-MAP-APP",
                        $"Application code '{map.ApplicationCode}' cannot map to more than one instrument code for outbound/both."));
                }
            }
        }

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Name)
                || string.IsNullOrWhiteSpace(rule.ConditionExpression)
                || string.IsNullOrWhiteSpace(rule.ActionExpression))
            {
                results.Add(RuleResult.HardStop("INST-RULE", "Each interface rule needs a name, condition, and action."));
                continue;
            }

            if (!Engine.RuleExpressionParser.TryParse(rule.ConditionExpression, out _, out var condError))
            {
                results.Add(RuleResult.HardStop("INST-RULE-COND", $"Rule '{rule.Name}' condition is invalid: {condError}"));
            }

            if (!InstrumentRuleActions.TryParse(rule.ActionExpression, out _, out var actError))
            {
                results.Add(RuleResult.HardStop("INST-RULE-ACT", $"Rule '{rule.Name}' action is invalid: {actError}"));
            }
        }

        return new RuleEvaluation(results);
    }
}
