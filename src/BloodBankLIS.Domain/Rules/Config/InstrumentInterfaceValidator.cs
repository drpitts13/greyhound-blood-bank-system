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
        IReadOnlyList<InstrumentValueTranslation> translations,
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

        if (entity.InterpretationMatch == InstrumentInterpretationMatch.AnalyteInList
            && InstrumentInterpretationClassifier.ParseCodes(entity.InterpretationAnalyteCodes).Count == 0)
        {
            results.Add(RuleResult.HardStop(
                "INST-INTERP-CODES",
                "List at least one instrument analyte code that identifies an interpretation record."));
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

        results.AddRange(ValidateTranslations(translations));

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

    /// <summary>
    /// Translation rows: a known field, both values, one instrument code per LIS test sent to the instrument, a test
    /// limit only on value, interpretation, and error rows, and Hold, Warn, or Ignore for error codes. Whether a local
    /// test or subtest exists is checked by the admin service, which can see the catalog.
    /// </summary>
    public static IReadOnlyList<RuleResult> ValidateTranslations(IReadOnlyList<InstrumentValueTranslation> translations)
    {
        var results = new List<RuleResult>();
        var outbound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in translations)
        {
            var field = InstrumentTranslationKeys.Find(row.DataItemKey);
            if (field is null)
            {
                results.Add(RuleResult.HardStop("INST-XLAT-KEY", $"Unknown translation field '{row.DataItemKey}'."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.InstrumentValue) || string.IsNullOrWhiteSpace(row.LocalValue))
            {
                results.Add(RuleResult.HardStop("INST-XLAT", $"{field.DisplayName}: each row needs an instrument value and a local value."));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(row.Context) && !field.AllowsContext)
            {
                results.Add(RuleResult.HardStop("INST-XLAT-CONTEXT", $"{field.DisplayName} rows apply to every test; remove the test limit on '{row.InstrumentValue}'."));
            }

            if (row.DataItemKey == InstrumentTranslationKeys.ErrorCode
                && !Enum.TryParse<InstrumentErrorAction>(row.LocalValue.Trim(), ignoreCase: true, out _))
            {
                results.Add(RuleResult.HardStop("INST-XLAT-ACTION", $"Error code '{row.InstrumentValue}' must translate to Hold, Warn, or Ignore."));
            }

            if (row.DataItemKey == InstrumentTranslationKeys.OrderTestCode
                && row.Direction is InterfaceTranslationDirection.Outbound or InterfaceTranslationDirection.Both
                && !outbound.Add(row.LocalValue.Trim()))
            {
                results.Add(RuleResult.HardStop("INST-XLAT-OUT", $"Test '{row.LocalValue}' can be sent to the instrument under only one code."));
            }
        }

        return results;
    }
}
