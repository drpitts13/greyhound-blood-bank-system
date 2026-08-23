using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules.Engine;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules.Config;

/// <summary>
/// Pure validators for admin configuration. They return a <see cref="RuleEvaluation"/> so
/// the application layer can persist drafts with warnings but block activation on any
/// hard stop. Duplicate/reference checks that need the database are passed in as flags by
/// the calling service (keeps the rules pure and unit-testable).
/// </summary>
public static class TestDefinitionValidator
{
    public static RuleEvaluation Validate(
        TestDefinition d,
        bool duplicateActiveCode,
        IReadOnlySet<string>? activeSubtestCodes = null,
        IReadOnlySet<string>? activeBloodAttributeCodes = null,
        IReadOnlySet<string>? activeSpecimenTypeCodes = null,
        IReadOnlyDictionary<string, PhaseDefinition>? phasesByCode = null)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(d.Code))
        {
            results.Add(RuleResult.HardStop("TESTDEF.CODE.REQUIRED", "Test code is required."));
        }

        if (string.IsNullOrWhiteSpace(d.Name))
        {
            results.Add(RuleResult.HardStop("TESTDEF.NAME.REQUIRED", "Test name is required."));
        }

        if (duplicateActiveCode)
        {
            results.Add(RuleResult.HardStop("TESTDEF.CODE.DUPLICATE", $"Another active test definition already uses code '{d.Code}'."));
        }

        if (d.ResultValueType == ResultValueType.Coded && string.IsNullOrWhiteSpace(d.AllowedResultValues))
        {
            results.Add(RuleResult.Warning("TESTDEF.ALLOWED.MISSING", "A coded test has no allowed result values configured."));
        }

        if (UsesPanelSubtests(d.ResultValueType))
        {
            var assignments = PanelSubtestAssignments.Parse(d.PanelSubtestsJson);
            if (assignments.Count == 0)
            {
                results.Add(RuleResult.HardStop("TESTDEF.PANEL.SUBTESTS.MISSING", "Panel tests must define at least one subtest for result entry."));
            }
            else if (assignments.Any(s => string.IsNullOrWhiteSpace(s.SubtestCode)))
            {
                results.Add(RuleResult.HardStop("TESTDEF.PANEL.CODE.REQUIRED", "Every panel subtest must reference a subtest code."));
            }
            else if (assignments.GroupBy(s => s.SubtestCode.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            {
                results.Add(RuleResult.HardStop("TESTDEF.PANEL.CODE.DUPLICATE", "Panel subtest codes must be unique."));
            }
            else if (activeSubtestCodes is not null)
            {
                foreach (var code in assignments.Select(a => a.SubtestCode.Trim()))
                {
                    if (!activeSubtestCodes.Contains(code))
                    {
                        results.Add(RuleResult.HardStop("TESTDEF.PANEL.SUBTEST.MISSING",
                            $"Panel references subtest '{code}' which is not in the active subtest catalog."));
                    }
                }
            }

            if (phasesByCode is not null)
            {
                foreach (var assignment in assignments)
                {
                    foreach (var raw in assignment.PhaseCodes ?? Array.Empty<string>())
                    {
                        var phaseCode = raw.Trim();
                        if (phaseCode.Length == 0)
                        {
                            continue;
                        }

                        if (!phasesByCode.ContainsKey(phaseCode))
                        {
                            results.Add(RuleResult.HardStop("TESTDEF.PANEL.PHASE.MISSING",
                                $"Panel subtest '{assignment.SubtestCode}' references phase '{phaseCode}' which is not in the active phase catalog."));
                        }
                    }
                }
            }

            var logicRows = InterpretationLogicDefinitions.Parse(d.InterpretationLogicJson);
            var assignedCodes = assignments.Select(a => a.SubtestCode.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var assignedPhasesBySubtest = assignments.ToDictionary(
                a => a.SubtestCode.Trim(),
                a => (a.PhaseCodes ?? Array.Empty<string>())
                    .Select(p => p.Trim())
                    .Where(p => p.Length > 0)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

            foreach (var row in logicRows)
            {
                foreach (var expectationKey in row.SubtestExpectations.Keys)
                {
                    if (PhaseResultKeys.TrySplit(expectationKey, out var subtestCode, out var phaseCode))
                    {
                        if (!assignedCodes.Contains(subtestCode))
                        {
                            results.Add(RuleResult.HardStop("TESTDEF.LOGIC.SUBTEST.UNASSIGNED",
                                $"Logic row '{row.Label}' references unassigned subtest '{subtestCode}'."));
                            continue;
                        }

                        if (assignedPhasesBySubtest.TryGetValue(subtestCode, out var phases)
                            && phases.Count > 0
                            && !phases.Contains(phaseCode))
                        {
                            results.Add(RuleResult.HardStop("TESTDEF.LOGIC.PHASE.UNASSIGNED",
                                $"Logic row '{row.Label}' references phase '{phaseCode}' not assigned to '{subtestCode}'."));
                        }

                        if (phasesByCode is not null
                            && phasesByCode.TryGetValue(phaseCode, out var phase)
                            && (phase.IsCheckCell || !phase.IncludeInInterpretation))
                        {
                            results.Add(RuleResult.HardStop("TESTDEF.LOGIC.PHASE.CHECKCELL",
                                $"Logic row '{row.Label}' cannot reference check-cell phase '{phaseCode}'."));
                        }
                    }
                    else if (!assignedCodes.Contains(expectationKey))
                    {
                        results.Add(RuleResult.HardStop("TESTDEF.LOGIC.SUBTEST.UNASSIGNED",
                            $"Logic row '{row.Label}' references unassigned subtest '{expectationKey}'."));
                    }
                }
            }

            if (d.ResultValueType == ResultValueType.AboRh && logicRows.Count == 0)
            {
                results.Add(RuleResult.Warning("TESTDEF.LOGIC.MISSING",
                    "ABO/Rh test has no interpretation logic table configured."));
            }

            if (d.ResultValueType == ResultValueType.Subtest && logicRows.Count == 0)
            {
                results.Add(RuleResult.Warning("TESTDEF.LOGIC.MISSING",
                    "Subtest panel has no interpretation logic table configured."));
            }
        }

        if (d.ResultValueType == ResultValueType.BloodAttribute)
        {
            if (d.BloodAttributeScopeKind is null)
            {
                results.Add(RuleResult.HardStop("TESTDEF.BLOODATTR.KIND.MISSING",
                    "Blood attribute tests must specify whether scoped codes are antigens or antibodies."));
            }

            var scope = BloodAttributeScope.Parse(d.BloodAttributeScopeJson);
            if (scope.Count == 0)
            {
                results.Add(RuleResult.HardStop("TESTDEF.BLOODATTR.SCOPE.MISSING",
                    "Blood attribute tests must define at least one catalog code in scope."));
            }
            else if (activeBloodAttributeCodes is not null)
            {
                foreach (var code in scope.Select(s => s.Code))
                {
                    if (!activeBloodAttributeCodes.Contains(code))
                    {
                        results.Add(RuleResult.HardStop("TESTDEF.BLOODATTR.SCOPE.MISSING",
                            $"Blood attribute scope references '{code}' which is not in the active catalog."));
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(d.RequiredSpecimenType) && activeSpecimenTypeCodes is not null)
        {
            var required = d.RequiredSpecimenType.Trim();
            if (!activeSpecimenTypeCodes.Contains(required))
            {
                results.Add(RuleResult.HardStop("TESTDEF.SPECTYPE.MISSING",
                    $"Required specimen type '{required}' is not in the active specimen type catalog."));
            }
        }

        return new RuleEvaluation(results);
    }

    public static bool UsesPanelSubtests(ResultValueType valueType) =>
        valueType is ResultValueType.AboRh
            or ResultValueType.Subtest
            or ResultValueType.Crossmatch
            or ResultValueType.ComplexCrossmatch;

    public static bool IsCrossmatchResultType(ResultValueType valueType) =>
        valueType is ResultValueType.Crossmatch or ResultValueType.ComplexCrossmatch;
}

public static class BloodAttributeDefinitionValidator
{
    public static RuleEvaluation Validate(BloodAttributeDefinition d, bool duplicateActiveCode)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(d.Code))
        {
            results.Add(RuleResult.HardStop("BLOODATTR.CODE.REQUIRED", "Antigen code is required."));
        }

        if (string.IsNullOrWhiteSpace(d.Name))
        {
            results.Add(RuleResult.HardStop("BLOODATTR.NAME.REQUIRED", "Display name is required."));
        }

        if (string.IsNullOrWhiteSpace(d.AntibodyName))
        {
            results.Add(RuleResult.HardStop("BLOODATTR.ABNAME.REQUIRED", "Antibody name is required."));
        }

        if (duplicateActiveCode)
        {
            results.Add(RuleResult.HardStop("BLOODATTR.CODE.DUPLICATE",
                $"Another active blood attribute already uses code '{d.Code}'."));
        }

        return new RuleEvaluation(results);
    }
}

public static class SpecimenTypeDefinitionValidator
{
    public static RuleEvaluation Validate(
        SpecimenTypeDefinition d,
        bool duplicateActiveCode,
        IReadOnlySet<string>? activeTestCodes = null)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(d.Code))
        {
            results.Add(RuleResult.HardStop("SPECTYPE.CODE.REQUIRED", "Specimen type code is required."));
        }

        if (string.IsNullOrWhiteSpace(d.Description))
        {
            results.Add(RuleResult.HardStop("SPECTYPE.DESC.REQUIRED", "Specimen type description is required."));
        }

        if (duplicateActiveCode)
        {
            results.Add(RuleResult.HardStop("SPECTYPE.CODE.DUPLICATE",
                $"Another active specimen type already uses code '{d.Code}'."));
        }

        if (activeTestCodes is not null)
        {
            foreach (var code in SpecimenTypeExcludedTests.Parse(d.ExcludedTestCodesJson))
            {
                if (!activeTestCodes.Contains(code))
                {
                    results.Add(RuleResult.HardStop("SPECTYPE.EXCLUDED.TEST.MISSING",
                        $"Excluded test '{code}' is not in the active test catalog."));
                }
            }
        }

        return new RuleEvaluation(results);
    }
}

public static class PhaseDefinitionValidator
{
    public static RuleEvaluation Validate(PhaseDefinition p, bool duplicateActiveCode)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(p.Code))
        {
            results.Add(RuleResult.HardStop("PHASE.CODE.REQUIRED", "Phase code is required."));
        }

        if (string.IsNullOrWhiteSpace(p.Name))
        {
            results.Add(RuleResult.HardStop("PHASE.NAME.REQUIRED", "Phase name is required."));
        }

        if (duplicateActiveCode)
        {
            results.Add(RuleResult.HardStop("PHASE.CODE.DUPLICATE",
                $"Another active phase already uses code '{p.Code}'."));
        }

        if (p.IsCheckCell)
        {
            if (p.IncludeInInterpretation)
            {
                results.Add(RuleResult.HardStop("PHASE.CHECKCELL.INTERP",
                    "Check-cell phases cannot be included in interpretation."));
            }

            if (string.IsNullOrWhiteSpace(p.ValidatesPhaseCode))
            {
                results.Add(RuleResult.Warning("PHASE.CHECKCELL.VALIDATES",
                    "Check-cell phases should specify which phase they validate (typically AHG)."));
            }
        }

        return new RuleEvaluation(results);
    }
}

public static class SubtestDefinitionValidator
{
    public static RuleEvaluation Validate(SubtestDefinition s, bool duplicateActiveCode)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(s.Code))
        {
            results.Add(RuleResult.HardStop("SUBTEST.CODE.REQUIRED", "Subtest code is required."));
        }

        if (string.IsNullOrWhiteSpace(s.Name))
        {
            results.Add(RuleResult.HardStop("SUBTEST.NAME.REQUIRED", "Subtest name is required."));
        }

        if (duplicateActiveCode)
        {
            results.Add(RuleResult.HardStop("SUBTEST.CODE.DUPLICATE", $"Another active subtest already uses code '{s.Code}'."));
        }

        var choices = SubtestChoiceDefinitions.Parse(s.ChoicesJson);
        if (s.ResultType is SubtestResultType.GradedReaction or SubtestResultType.PickList)
        {
            if (choices.Count == 0)
            {
                results.Add(RuleResult.HardStop("SUBTEST.CHOICES.MISSING", $"{s.ResultType} subtests require at least one choice."));
            }
            else if (choices.Any(c => string.IsNullOrWhiteSpace(c.Code)))
            {
                results.Add(RuleResult.HardStop("SUBTEST.CHOICE.CODE.REQUIRED", "Every choice must have a code."));
            }
            else if (choices.GroupBy(c => c.Code.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            {
                results.Add(RuleResult.HardStop("SUBTEST.CHOICE.DUPLICATE", "Choice codes must be unique."));
            }
            else if (s.ResultType == SubtestResultType.GradedReaction
                     && choices.Any(c => c.Polarity is null))
            {
                results.Add(RuleResult.HardStop("SUBTEST.POLARITY.REQUIRED",
                    "Graded-reaction choices require a positive/negative/neutral polarity."));
            }
        }

        return new RuleEvaluation(results);
    }
}

public static class TestGrouperValidator
{
    public static RuleEvaluation Validate(
        TestGrouper g,
        bool duplicateActiveCode,
        IReadOnlySet<string>? activeTestCodes = null)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(g.Code))
        {
            results.Add(RuleResult.HardStop("GROUPER.CODE.REQUIRED", "Grouper code is required."));
        }

        if (string.IsNullOrWhiteSpace(g.Name))
        {
            results.Add(RuleResult.HardStop("GROUPER.NAME.REQUIRED", "Grouper name is required."));
        }

        if (duplicateActiveCode)
        {
            results.Add(RuleResult.HardStop("GROUPER.CODE.DUPLICATE", $"Another active grouper already uses code '{g.Code}'."));
        }

        var members = TestGrouperMembers.Parse(g.MemberTestsJson);
        if (members.Count == 0)
        {
            results.Add(RuleResult.HardStop("GROUPER.MEMBERS.MISSING", "Test grouper must include at least one member test."));
        }
        else if (members.Any(m => string.IsNullOrWhiteSpace(m.TestCode)))
        {
            results.Add(RuleResult.HardStop("GROUPER.MEMBER.CODE.REQUIRED", "Every member must reference a test code."));
        }
        else if (members.GroupBy(m => m.TestCode.Trim(), StringComparer.OrdinalIgnoreCase).Any(g2 => g2.Count() > 1))
        {
            results.Add(RuleResult.HardStop("GROUPER.MEMBER.DUPLICATE", "Member test codes must be unique."));
        }
        else if (activeTestCodes is not null)
        {
            foreach (var code in members.Select(m => m.TestCode.Trim()))
            {
                if (!activeTestCodes.Contains(code))
                {
                    results.Add(RuleResult.HardStop("GROUPER.MEMBER.MISSING",
                        $"Grouper references test '{code}' which is not in the active test catalog."));
                }
            }
        }

        return new RuleEvaluation(results);
    }
}

public static class ReflexRuleValidator
{
    public static RuleEvaluation Validate(
        ReflexRule rule,
        bool duplicateActiveCode,
        bool duplicateActiveTriple,
        IReadOnlySet<string>? activeTestCodes = null)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(rule.Code))
        {
            results.Add(RuleResult.HardStop("REFLEX.CODE.REQUIRED", "Reflex rule code is required."));
        }

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            results.Add(RuleResult.HardStop("REFLEX.NAME.REQUIRED", "Reflex rule name is required."));
        }

        if (duplicateActiveCode)
        {
            results.Add(RuleResult.HardStop("REFLEX.CODE.DUPLICATE",
                $"Another active reflex rule already uses code '{rule.Code}'."));
        }

        if (string.IsNullOrWhiteSpace(rule.TriggerTestCode))
        {
            results.Add(RuleResult.HardStop("REFLEX.TRIGGER.REQUIRED", "Trigger test code is required."));
        }

        if (string.IsNullOrWhiteSpace(rule.TriggerResultValue))
        {
            results.Add(RuleResult.HardStop("REFLEX.VALUE.REQUIRED", "Trigger result value is required."));
        }

        if (string.IsNullOrWhiteSpace(rule.ReflexTestCode))
        {
            results.Add(RuleResult.HardStop("REFLEX.REFLEX.REQUIRED", "Reflex test code is required."));
        }

        if (!string.IsNullOrWhiteSpace(rule.TriggerTestCode)
            && !string.IsNullOrWhiteSpace(rule.ReflexTestCode)
            && string.Equals(rule.TriggerTestCode.Trim(), rule.ReflexTestCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            results.Add(RuleResult.HardStop("REFLEX.SELF", "Trigger and reflex test codes must be different."));
        }

        if (duplicateActiveTriple)
        {
            results.Add(RuleResult.HardStop("REFLEX.TRIPLE.DUPLICATE",
                "An active reflex rule already maps this trigger test, result value, and reflex test."));
        }

        if (activeTestCodes is not null)
        {
            if (!string.IsNullOrWhiteSpace(rule.TriggerTestCode)
                && !activeTestCodes.Contains(rule.TriggerTestCode.Trim()))
            {
                results.Add(RuleResult.HardStop("REFLEX.TRIGGER.MISSING",
                    $"Trigger test '{rule.TriggerTestCode}' is not in the active test catalog."));
            }

            if (!string.IsNullOrWhiteSpace(rule.ReflexTestCode)
                && !activeTestCodes.Contains(rule.ReflexTestCode.Trim()))
            {
                results.Add(RuleResult.HardStop("REFLEX.REFLEX.MISSING",
                    $"Reflex test '{rule.ReflexTestCode}' is not in the active test catalog."));
            }
        }

        return new RuleEvaluation(results);
    }
}

public static class ProductDefinitionValidator
{
    public static RuleEvaluation Validate(ProductType p, bool duplicateActiveCode)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(p.ProductCode))
        {
            results.Add(RuleResult.HardStop("PRODUCT.CODE.REQUIRED", "Product code is required."));
        }

        if (string.IsNullOrWhiteSpace(p.Name))
        {
            results.Add(RuleResult.HardStop("PRODUCT.NAME.REQUIRED", "Product name is required."));
        }

        if (duplicateActiveCode)
        {
            results.Add(RuleResult.HardStop("PRODUCT.CODE.DUPLICATE", $"Another active product already uses code '{p.ProductCode}'."));
        }

        if (p.DefaultShelfLifeHours is <= 0)
        {
            results.Add(RuleResult.HardStop("PRODUCT.SHELFLIFE.INVALID", "Shelf life must be a positive number of hours."));
        }

        // Unsafe-rule guard: a crossmatched red-cell product should enforce ABO matching.
        if (p.RequiresCrossmatch && !p.RequiresAboMatch)
        {
            results.Add(RuleResult.Warning("PRODUCT.ABO.UNSAFE", "Product requires crossmatch but is not configured to require ABO matching."));
        }

        return new RuleEvaluation(results);
    }
}

public static class ExpirationModificationCodeValidator
{
    public static RuleEvaluation Validate(ExpirationModificationCode code, bool duplicateActiveCode)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(code.Code))
        {
            results.Add(RuleResult.HardStop("EXPCODE.CODE.REQUIRED", "An expiration modification code is required."));
        }

        if (code.OffsetAmount <= 0)
        {
            results.Add(RuleResult.HardStop("EXPCODE.AMOUNT.INVALID", "Offset amount must be a positive number."));
        }

        if (!Enum.IsDefined(code.OffsetUnit))
        {
            results.Add(RuleResult.HardStop("EXPCODE.UNIT.INVALID", "Offset unit must be Hours or Days."));
        }

        if (!Enum.IsDefined(code.RelativeTo))
        {
            results.Add(RuleResult.HardStop("EXPCODE.RELATIVE.INVALID", "Relative-to must be modification or collection date/time."));
        }

        if (duplicateActiveCode)
        {
            results.Add(RuleResult.HardStop("EXPCODE.CODE.DUPLICATE",
                "Another expiration modification code already uses this code."));
        }

        return new RuleEvaluation(results);
    }
}

public static class ModificationRuleValidator
{
    public static RuleEvaluation Validate(
        ModificationRule r,
        bool duplicateActiveTriple,
        bool? sourceProductActive = null,
        bool? targetProductActive = null,
        bool? expirationCodeActive = null)
    {
        var results = new List<RuleResult>();

        if (r.SourceProductTypeId <= 0)
        {
            results.Add(RuleResult.HardStop("MODRULE.SOURCE.REQUIRED", "A source product is required."));
        }

        if (r.TargetProductTypeId <= 0)
        {
            results.Add(RuleResult.HardStop("MODRULE.TARGET.REQUIRED", "A target product is required."));
        }

        if (r.ExpirationModificationCodeId <= 0)
        {
            results.Add(RuleResult.HardStop("MODRULE.EXPCODE.REQUIRED", "An expiration modification code is required."));
        }

        if (duplicateActiveTriple)
        {
            results.Add(RuleResult.HardStop("MODRULE.TRIPLE.DUPLICATE",
                "Another active modification rule already maps this source product, modification type, and target product."));
        }

        if (sourceProductActive == false)
        {
            results.Add(RuleResult.HardStop("MODRULE.SOURCE.INACTIVE", "The source product is not an active product definition."));
        }

        if (targetProductActive == false)
        {
            results.Add(RuleResult.HardStop("MODRULE.TARGET.INACTIVE", "The target product is not an active product definition."));
        }

        if (expirationCodeActive == false)
        {
            results.Add(RuleResult.HardStop("MODRULE.EXPCODE.INACTIVE", "The expiration modification code is not active."));
        }

        if (r.ModificationType is ModificationType.Divide or ModificationType.Pool
            && r.SourceProductTypeId > 0 && r.TargetProductTypeId > 0 && r.SourceProductTypeId == r.TargetProductTypeId)
        {
            results.Add(RuleResult.Warning("MODRULE.SAMEPRODUCT",
                "Source and target product are the same; confirm this is intended for this modification type."));
        }

        return new RuleEvaluation(results);
    }
}

public static class Hl7EndpointValidator
{
    public static RuleEvaluation Validate(InterfaceEndpoint e, bool duplicateActiveName, bool duplicateActiveHostPort)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(e.Name))
        {
            results.Add(RuleResult.HardStop("HL7EP.NAME.REQUIRED", "Endpoint name is required."));
        }

        if (string.IsNullOrWhiteSpace(e.MessageTypes))
        {
            results.Add(RuleResult.HardStop("HL7EP.MSGTYPES.REQUIRED", "At least one message type is required."));
        }

        if (e.Transport == InterfaceTransport.Mllp)
        {
            if (string.IsNullOrWhiteSpace(e.Host))
            {
                results.Add(RuleResult.HardStop("HL7EP.HOST.REQUIRED", "MLLP endpoints require a host."));
            }

            if (e.Port is null or < 1 or > 65535)
            {
                results.Add(RuleResult.HardStop("HL7EP.PORT.INVALID", "MLLP endpoints require a valid port (1-65535)."));
            }
        }

        if (duplicateActiveName)
        {
            results.Add(RuleResult.HardStop("HL7EP.NAME.DUPLICATE", $"Another endpoint already uses the name '{e.Name}'."));
        }

        if (duplicateActiveHostPort)
        {
            results.Add(RuleResult.Warning("HL7EP.HOSTPORT.DUPLICATE", "Another enabled endpoint shares this host and port."));
        }

        if (e.AckTimeoutSeconds is < 0)
        {
            results.Add(RuleResult.HardStop("HL7EP.ACK.INVALID", "ACK timeout cannot be negative."));
        }

        if (e.MaxRetryCount is < 0)
        {
            results.Add(RuleResult.HardStop("HL7EP.RETRY.INVALID", "Max retry count cannot be negative."));
        }

        return new RuleEvaluation(results);
    }
}

public static class RuleDefinitionValidator
{
    /// <summary>
    /// Validates a configurable order/test rule. The condition and action expressions must
    /// parse and may only reference attributes the rule's level can supply, otherwise the
    /// rule could never fire. Test codes are checked against the catalog as warnings only,
    /// so a rule may be authored before its target test exists.
    /// </summary>
    /// <param name="knownTestCodes">Active test definition and test grouper codes, or null to skip the check.</param>
    public static RuleEvaluation Validate(
        RuleDefinition rule,
        bool duplicateActiveCode,
        IReadOnlySet<string>? knownTestCodes = null)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(rule.Code))
        {
            results.Add(RuleResult.HardStop("RULE.CODE.REQUIRED", "Rule code is required."));
        }

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            results.Add(RuleResult.HardStop("RULE.NAME.REQUIRED", "Rule name is required."));
        }

        if (duplicateActiveCode)
        {
            results.Add(RuleResult.HardStop("RULE.CODE.DUPLICATE",
                $"Another active rule already uses code '{rule.Code}'."));
        }

        ValidateCondition(rule, results);
        var actions = ValidateActions(rule, results);
        ValidateTestCodes(actions, knownTestCodes, results);

        return new RuleEvaluation(results);
    }

    private static void ValidateCondition(RuleDefinition rule, List<RuleResult> results)
    {
        if (string.IsNullOrWhiteSpace(rule.ConditionExpression))
        {
            results.Add(RuleResult.HardStop("RULE.CONDITION.REQUIRED", "A condition is required."));
            return;
        }

        if (!RuleExpressionParser.TryParse(rule.ConditionExpression, out var node, out var syntaxError))
        {
            results.Add(RuleResult.HardStop("RULE.CONDITION.SYNTAX", $"Condition is not valid: {syntaxError}"));
            return;
        }

        foreach (var unknown in RuleAttributeCatalog.FindUnknownReferences(node, rule.Level))
        {
            results.Add(RuleResult.HardStop("RULE.CONDITION.ATTRIBUTE", unknown));
        }
    }

    private static IReadOnlyList<RuleActionInstruction> ValidateActions(RuleDefinition rule, List<RuleResult> results)
    {
        if (string.IsNullOrWhiteSpace(rule.ActionExpression))
        {
            results.Add(RuleResult.HardStop("RULE.ACTION.REQUIRED", "At least one action is required."));
            return Array.Empty<RuleActionInstruction>();
        }

        if (!RuleActionParser.TryParse(rule.ActionExpression, rule.Level, out var actions, out var actionError))
        {
            var code = actionError is not null && actionError.Contains("only available to", StringComparison.OrdinalIgnoreCase)
                ? "RULE.ACTION.LEVEL"
                : "RULE.ACTION.SYNTAX";
            results.Add(RuleResult.HardStop(code, $"Actions are not valid: {actionError}"));
            return Array.Empty<RuleActionInstruction>();
        }

        var added = actions.Where(a => a.Kind == RuleActionKind.AddTest).Select(a => a.TestCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cancelled = actions.Where(a => a.Kind == RuleActionKind.CancelTest).Select(a => a.TestCode);
        foreach (var conflict in cancelled.Where(added.Contains).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            results.Add(RuleResult.HardStop("RULE.ACTION.SELF",
                $"Test '{conflict}' is both added and cancelled by this rule."));
        }

        return actions;
    }

    private static void ValidateTestCodes(
        IReadOnlyList<RuleActionInstruction> actions,
        IReadOnlySet<string>? knownTestCodes,
        List<RuleResult> results)
    {
        if (knownTestCodes is null)
        {
            return;
        }

        var referenced = actions
            .Where(a => a.Kind is RuleActionKind.AddTest or RuleActionKind.CancelTest)
            .Select(a => a.TestCode)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var code in referenced.Where(c => !knownTestCodes.Contains(c)))
        {
            results.Add(RuleResult.Warning("RULE.TEST.UNKNOWN",
                $"Test '{code}' is not in the active test catalog. The action will be skipped until it exists."));
        }
    }
}
