using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules.Engine;

/// <summary>One parsed action call, e.g. <c>addTest('WEAKD')</c>.</summary>
public sealed record RuleActionInstruction(RuleActionKind Kind, string Argument)
{
    /// <summary>Test code for AddTest/CancelTest, normalized to upper case.</summary>
    public string TestCode => Argument.Trim().ToUpperInvariant();

    public override string ToString() => $"{Name(Kind)}('{Argument}')";

    public static string Name(RuleActionKind kind) => kind switch
    {
        RuleActionKind.AddTest => "addTest",
        RuleActionKind.CancelTest => "cancelTest",
        RuleActionKind.Warn => "warn",
        RuleActionKind.Block => "block",
        _ => kind.ToString()
    };
}

public sealed record RuleActionDescriptor(
    RuleActionKind Kind,
    string Name,
    RuleLevel? RestrictedTo,
    string Description,
    string Example);

/// <summary>
/// Parses a rule's action expression: a semicolon-separated list of single-argument
/// calls, e.g. <c>cancelTest('TS'); addTest('TSNEO')</c>.
/// </summary>
public static class RuleActionParser
{
    public static IReadOnlyList<RuleActionDescriptor> Descriptors { get; } = new List<RuleActionDescriptor>
    {
        new(RuleActionKind.AddTest, "addTest", null,
            "Add a test line to the order when it is not already present.", "addTest('WEAKD')"),
        new(RuleActionKind.CancelTest, "cancelTest", null,
            "Cancel a pending test line on the order.", "cancelTest('TS')"),
        new(RuleActionKind.Warn, "warn", null,
            "Surface an overridable warning to the operator.", "warn('Neonatal protocol applies')"),
        new(RuleActionKind.Block, "block", RuleLevel.Order,
            "Hard-stop the order. Order-level rules only.", "block('Specimen type not allowed')")
    };

    public static IReadOnlyList<RuleActionDescriptor> For(RuleLevel level) =>
        Descriptors.Where(d => d.RestrictedTo is null || d.RestrictedTo == level).ToList();

    public static IReadOnlyList<RuleActionInstruction> Parse(string? text, RuleLevel level)
    {
        var tokens = RuleLexer.Tokenize(text);
        var actions = new List<RuleActionInstruction>();
        var index = 0;

        while (tokens[index].Kind != RuleTokenKind.End)
        {
            if (tokens[index].Kind == RuleTokenKind.Semicolon)
            {
                index++;
                continue;
            }

            var nameToken = tokens[index];
            if (nameToken.Kind != RuleTokenKind.Identifier)
            {
                throw new RuleSyntaxException($"Expected an action name but found '{nameToken.Text}'.", nameToken.Position);
            }

            var descriptor = Descriptors.FirstOrDefault(d =>
                string.Equals(d.Name, nameToken.Text, StringComparison.OrdinalIgnoreCase));
            if (descriptor is null)
            {
                throw new RuleSyntaxException(
                    $"Unknown action '{nameToken.Text}'. Available: {string.Join(", ", For(level).Select(d => d.Name))}.",
                    nameToken.Position);
            }

            if (descriptor.RestrictedTo is not null && descriptor.RestrictedTo != level)
            {
                throw new RuleSyntaxException(
                    $"Action '{descriptor.Name}' is only available to {descriptor.RestrictedTo}-level rules.",
                    nameToken.Position);
            }

            index++;
            if (tokens[index].Kind != RuleTokenKind.OpenParen)
            {
                throw new RuleSyntaxException($"Expected '(' after '{descriptor.Name}'.", tokens[index].Position);
            }

            index++;
            var argumentToken = tokens[index];
            if (argumentToken.Kind != RuleTokenKind.Text)
            {
                throw new RuleSyntaxException(
                    $"'{descriptor.Name}' takes one quoted argument.", argumentToken.Position);
            }

            if (string.IsNullOrWhiteSpace(argumentToken.Text))
            {
                throw new RuleSyntaxException(
                    $"'{descriptor.Name}' argument cannot be empty.", argumentToken.Position);
            }

            index++;
            if (tokens[index].Kind != RuleTokenKind.CloseParen)
            {
                throw new RuleSyntaxException(
                    $"Expected ')' to close '{descriptor.Name}'.", tokens[index].Position);
            }

            index++;
            actions.Add(new RuleActionInstruction(descriptor.Kind, argumentToken.Text.Trim()));

            if (tokens[index].Kind is not (RuleTokenKind.Semicolon or RuleTokenKind.End))
            {
                throw new RuleSyntaxException(
                    $"Expected ';' between actions but found '{tokens[index].Text}'.", tokens[index].Position);
            }
        }

        if (actions.Count == 0)
        {
            throw new RuleSyntaxException("At least one action is required.", 0);
        }

        return actions;
    }

    public static bool TryParse(
        string? text,
        RuleLevel level,
        out IReadOnlyList<RuleActionInstruction> actions,
        out string? error)
    {
        try
        {
            actions = Parse(text, level);
            error = null;
            return true;
        }
        catch (RuleSyntaxException ex)
        {
            actions = Array.Empty<RuleActionInstruction>();
            error = ex.Message;
            return false;
        }
    }
}
