using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules.Engine;

public sealed record RuleAttributeDescriptor(
    string Path,
    RuleValueKind Kind,
    RuleLevel MinimumLevel,
    string Description,
    string Example);

public sealed record RuleFunctionDescriptor(
    string Name,
    int Arity,
    RuleValueKind ReturnKind,
    RuleLevel MinimumLevel,
    string Description,
    string Example)
{
    /// <summary>Alternate spellings accepted by the parser, e.g. <c>hasTest</c> for <c>order.hasTest</c>.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
}

/// <summary>
/// The single whitelist of attributes and functions a rule condition may reference.
/// Backs both authoring-time validation and the admin UI reference panel, so a rule
/// can never be activated against a path the fact sources do not supply.
/// Order-level attributes are also available to test-level rules; the reverse is not true.
/// </summary>
public static class RuleAttributeCatalog
{
    private static readonly IReadOnlyList<RuleAttributeDescriptor> AllAttributes = new List<RuleAttributeDescriptor>
    {
        // ---- Patient ----
        new("patient.ageDays", RuleValueKind.Number, RuleLevel.Order,
            "Patient age in whole days at evaluation time.", "patient.ageDays < 1"),
        new("patient.ageMonths", RuleValueKind.Number, RuleLevel.Order,
            "Patient age in whole months.", "patient.ageMonths <= 4"),
        new("patient.ageYears", RuleValueKind.Number, RuleLevel.Order,
            "Patient age in whole years.", "patient.ageYears >= 18"),
        new("patient.sex", RuleValueKind.Text, RuleLevel.Order,
            "Unknown, Male, Female or Other.", "patient.sex = 'Female'"),
        new("patient.abo", RuleValueKind.Text, RuleLevel.Order,
            "Current historical ABO group: Unknown, O, A, B or AB.", "patient.abo = 'O'"),
        new("patient.rh", RuleValueKind.Text, RuleLevel.Order,
            "Current historical Rh(D): Unknown, Positive or Negative.", "patient.rh = 'Negative'"),
        new("patient.bloodType", RuleValueKind.Text, RuleLevel.Order,
            "Current historical ABO/Rh as text, e.g. 'A Negative'.", "patient.bloodType = 'A Negative'"),

        // ---- Order ----
        new("order.date", RuleValueKind.Date, RuleLevel.Order,
            "Order date/time in UTC.", "order.date >= '2026-01-01'"),
        new("order.priority", RuleValueKind.Text, RuleLevel.Order,
            "Routine, Stat, Timed, Urgent, EmergencyRelease, MassiveTransfusionProtocol, PreOp or OutpatientScheduled.",
            "order.priority = 'Stat'"),
        new("order.specimenType", RuleValueKind.Text, RuleLevel.Order,
            "Specimen type code of the order's primary specimen, e.g. EDTA.", "order.specimenType = 'EDTA'"),
        new("order.category", RuleValueKind.Text, RuleLevel.Order,
            "Test, Product or Mixed.", "order.category = 'Product'"),
        new("order.number", RuleValueKind.Text, RuleLevel.Order,
            "Placer order number.", "order.number CONTAINS 'ER'"),
        new("order.tests", RuleValueKind.List, RuleLevel.Order,
            "Test codes on the order.", "order.tests CONTAINS 'ABORH'"),
        new("order.productTypes", RuleValueKind.List, RuleLevel.Order,
            "Product codes on the order.", "order.productTypes CONTAINS 'E0336'"),

        // ---- Test (result verification only) ----
        new("test.code", RuleValueKind.Text, RuleLevel.Test,
            "Test code of the verified result.", "test.code = 'ABORH'"),
        new("test.result", RuleValueKind.Text, RuleLevel.Test,
            "Raw result value of the verified result.", "test.result = 'Positive'"),
        new("test.interpretation", RuleValueKind.Text, RuleLevel.Test,
            "Interpretation, derived from the result value when not entered explicitly, e.g. 'A Negative'.",
            "test.interpretation IN ('A Negative','O Negative')"),
        new("test.abo", RuleValueKind.Text, RuleLevel.Test,
            "ABO group interpreted from an ABO/Rh result.", "test.abo = 'AB'"),
        new("test.rh", RuleValueKind.Text, RuleLevel.Test,
            "Rh(D) interpreted from an ABO/Rh result.", "test.rh = 'Negative'"),
        new("test.status", RuleValueKind.Text, RuleLevel.Test,
            "Pending, Entered, Verified or Corrected.", "test.status = 'Verified'"),
        new("test.subtests", RuleValueKind.List, RuleLevel.Test,
            "Subtest codes present on a panel result.", "test.subtests CONTAINS 'Anti-D'")
    };

    private static readonly IReadOnlyList<RuleFunctionDescriptor> AllFunctions = new List<RuleFunctionDescriptor>
    {
        new("order.hasTest", 1, RuleValueKind.Boolean, RuleLevel.Order,
            "True when the order carries the given test code.", "order.hasTest('ABORH')")
        {
            Aliases = new[] { "hasTest" }
        },
        new("order.hasProduct", 1, RuleValueKind.Boolean, RuleLevel.Order,
            "True when the order carries the given product code.", "order.hasProduct('E0336')")
        {
            Aliases = new[] { "hasProduct" }
        },
        new("test.subtest", 1, RuleValueKind.Text, RuleLevel.Test,
            "Reaction grade recorded for a panel subtest code.", "test.subtest('Anti-D') = '0'")
        {
            Aliases = new[] { "subtest" }
        }
    };

    public static IReadOnlyList<RuleAttributeDescriptor> Attributes(RuleLevel level) =>
        AllAttributes.Where(a => IsAvailable(a.MinimumLevel, level)).ToList();

    public static IReadOnlyList<RuleFunctionDescriptor> Functions(RuleLevel level) =>
        AllFunctions.Where(f => IsAvailable(f.MinimumLevel, level)).ToList();

    public static bool IsKnownAttribute(string? path, RuleLevel level) =>
        ResolveAttribute(path, level) is not null;

    public static RuleAttributeDescriptor? ResolveAttribute(string? path, RuleLevel level)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalized = path.Trim();
        return AllAttributes.FirstOrDefault(a =>
            IsAvailable(a.MinimumLevel, level)
            && string.Equals(a.Path, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static RuleFunctionDescriptor? ResolveFunction(string? name, RuleLevel level)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalized = name.Trim();
        return AllFunctions.FirstOrDefault(f =>
            IsAvailable(f.MinimumLevel, level)
            && (string.Equals(f.Name, normalized, StringComparison.OrdinalIgnoreCase)
                || f.Aliases.Any(a => string.Equals(a, normalized, StringComparison.OrdinalIgnoreCase))));
    }

    /// <summary>
    /// Collects every reference in the parsed condition that the given level cannot supply.
    /// An empty result means the condition is safe to evaluate.
    /// </summary>
    public static IReadOnlyList<string> FindUnknownReferences(RuleExpressionNode? node, RuleLevel level)
    {
        if (node is null)
        {
            return Array.Empty<string>();
        }

        var collector = new UnknownReferenceCollector(level);
        node.Accept(collector);
        return collector.Errors;
    }

    private static bool IsAvailable(RuleLevel minimumLevel, RuleLevel level) =>
        minimumLevel == RuleLevel.Order || level == RuleLevel.Test;

    private sealed class UnknownReferenceCollector : IRuleExpressionVisitor<bool>
    {
        private readonly RuleLevel _level;
        private readonly List<string> _errors = new();

        public UnknownReferenceCollector(RuleLevel level) => _level = level;

        public IReadOnlyList<string> Errors => _errors;

        public bool VisitLiteral(RuleLiteralNode node) => true;

        public bool VisitAttribute(RuleAttributeNode node)
        {
            if (ResolveAttribute(node.Path, _level) is not null)
            {
                return true;
            }

            // Distinguish "does not exist" from "not available at this level".
            var existsElsewhere = AllAttributes.Any(a =>
                string.Equals(a.Path, node.Path, StringComparison.OrdinalIgnoreCase));
            _errors.Add(existsElsewhere
                ? $"'{node.Path}' is only available to test-level rules."
                : $"Unknown attribute '{node.Path}'.");
            return false;
        }

        public bool VisitFunction(RuleFunctionNode node)
        {
            foreach (var argument in node.Arguments)
            {
                argument.Accept(this);
            }

            var descriptor = ResolveFunction(node.Name, _level);
            if (descriptor is null)
            {
                var existsElsewhere = AllFunctions.Any(f =>
                    string.Equals(f.Name, node.Name, StringComparison.OrdinalIgnoreCase)
                    || f.Aliases.Any(a => string.Equals(a, node.Name, StringComparison.OrdinalIgnoreCase)));
                _errors.Add(existsElsewhere
                    ? $"'{node.Name}' is only available to test-level rules."
                    : $"Unknown function '{node.Name}'.");
                return false;
            }

            if (node.Arguments.Count != descriptor.Arity)
            {
                _errors.Add($"'{descriptor.Name}' takes {descriptor.Arity} argument(s) but got {node.Arguments.Count}.");
            }

            return true;
        }

        public bool VisitList(RuleListNode node)
        {
            foreach (var item in node.Items)
            {
                item.Accept(this);
            }

            return true;
        }

        public bool VisitUnary(RuleUnaryNode node) => node.Operand.Accept(this);

        public bool VisitBinary(RuleBinaryNode node)
        {
            node.Left.Accept(this);
            node.Right.Accept(this);
            return true;
        }
    }
}
