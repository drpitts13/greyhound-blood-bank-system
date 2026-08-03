using System.Globalization;

namespace BloodBankLIS.Domain.Rules.Engine;

/// <summary>
/// Recursive-descent parser for rule conditions. Precedence, loosest first:
/// OR, AND, NOT, comparison, primary.
/// </summary>
public static class RuleExpressionParser
{
    public static RuleExpressionNode Parse(string? text)
    {
        var tokens = RuleLexer.Tokenize(text);
        var state = new ParserState(tokens);
        if (state.Current.Kind == RuleTokenKind.End)
        {
            throw new RuleSyntaxException("Condition is empty.", 0);
        }

        var node = ParseOr(state);
        if (state.Current.Kind != RuleTokenKind.End)
        {
            throw new RuleSyntaxException($"Unexpected '{state.Current.Text}'.", state.Current.Position);
        }

        return node;
    }

    public static bool TryParse(string? text, out RuleExpressionNode? node, out string? error)
    {
        try
        {
            node = Parse(text);
            error = null;
            return true;
        }
        catch (RuleSyntaxException ex)
        {
            node = null;
            error = ex.Message;
            return false;
        }
    }

    private static RuleExpressionNode ParseOr(ParserState state)
    {
        var left = ParseAnd(state);
        while (state.Current.IsKeyword("OR"))
        {
            state.Advance();
            var right = ParseAnd(state);
            left = new RuleBinaryNode(RuleBinaryOperator.Or, left, right);
        }

        return left;
    }

    private static RuleExpressionNode ParseAnd(ParserState state)
    {
        var left = ParseNot(state);
        while (state.Current.IsKeyword("AND"))
        {
            state.Advance();
            var right = ParseNot(state);
            left = new RuleBinaryNode(RuleBinaryOperator.And, left, right);
        }

        return left;
    }

    private static RuleExpressionNode ParseNot(ParserState state)
    {
        if (state.Current.IsKeyword("NOT"))
        {
            state.Advance();
            return new RuleUnaryNode(RuleUnaryOperator.Not, ParseNot(state));
        }

        return ParseComparison(state);
    }

    private static RuleExpressionNode ParseComparison(ParserState state)
    {
        var left = ParsePrimary(state);

        if (state.Current.Kind == RuleTokenKind.Operator)
        {
            var op = state.Current.Text switch
            {
                "=" => RuleBinaryOperator.Equal,
                "!=" => RuleBinaryOperator.NotEqual,
                "<" => RuleBinaryOperator.LessThan,
                "<=" => RuleBinaryOperator.LessThanOrEqual,
                ">" => RuleBinaryOperator.GreaterThan,
                ">=" => RuleBinaryOperator.GreaterThanOrEqual,
                _ => throw new RuleSyntaxException(
                    $"Unsupported operator '{state.Current.Text}'.", state.Current.Position)
            };

            state.Advance();
            return new RuleBinaryNode(op, left, ParsePrimary(state));
        }

        if (state.Current.IsKeyword("IN"))
        {
            state.Advance();
            return new RuleBinaryNode(RuleBinaryOperator.In, left, ParseList(state));
        }

        if (state.Current.IsKeyword("CONTAINS"))
        {
            state.Advance();
            return new RuleBinaryNode(RuleBinaryOperator.Contains, left, ParsePrimary(state));
        }

        if (state.Current.IsKeyword("NOT"))
        {
            var notPosition = state.Current.Position;
            state.Advance();
            if (!state.Current.IsKeyword("IN"))
            {
                throw new RuleSyntaxException("Expected 'IN' after 'NOT'.", notPosition);
            }

            state.Advance();
            return new RuleBinaryNode(RuleBinaryOperator.NotIn, left, ParseList(state));
        }

        if (state.Current.IsKeyword("IS"))
        {
            state.Advance();
            if (state.Current.IsKeyword("NOT"))
            {
                state.Advance();
                Expect(state, t => t.IsKeyword("NULL"), "NULL");
                return new RuleUnaryNode(RuleUnaryOperator.IsNotNull, left);
            }

            Expect(state, t => t.IsKeyword("NULL"), "NULL");
            return new RuleUnaryNode(RuleUnaryOperator.IsNull, left);
        }

        return left;
    }

    private static RuleExpressionNode ParseList(ParserState state)
    {
        Expect(state, t => t.Kind == RuleTokenKind.OpenParen, "(");

        var items = new List<RuleExpressionNode>();
        if (state.Current.Kind != RuleTokenKind.CloseParen)
        {
            items.Add(ParseOr(state));
            while (state.Current.Kind == RuleTokenKind.Comma)
            {
                state.Advance();
                items.Add(ParseOr(state));
            }
        }

        Expect(state, t => t.Kind == RuleTokenKind.CloseParen, ")");
        return new RuleListNode(items);
    }

    private static RuleExpressionNode ParsePrimary(ParserState state)
    {
        var token = state.Current;

        switch (token.Kind)
        {
            case RuleTokenKind.OpenParen:
            {
                state.Advance();
                var inner = ParseOr(state);
                Expect(state, t => t.Kind == RuleTokenKind.CloseParen, ")");
                return inner;
            }

            case RuleTokenKind.Text:
                state.Advance();
                return new RuleLiteralNode(RuleValue.FromText(token.Text));

            case RuleTokenKind.Number:
                state.Advance();
                return new RuleLiteralNode(RuleValue.FromNumber(
                    decimal.Parse(token.Text, NumberStyles.Number, CultureInfo.InvariantCulture)));

            case RuleTokenKind.Keyword when token.IsKeyword("TRUE"):
                state.Advance();
                return new RuleLiteralNode(RuleValue.True);

            case RuleTokenKind.Keyword when token.IsKeyword("FALSE"):
                state.Advance();
                return new RuleLiteralNode(RuleValue.False);

            case RuleTokenKind.Keyword when token.IsKeyword("NULL"):
                state.Advance();
                return new RuleLiteralNode(RuleValue.Null);

            case RuleTokenKind.Keyword when token.IsKeyword("NOT"):
                state.Advance();
                return new RuleUnaryNode(RuleUnaryOperator.Not, ParsePrimary(state));

            case RuleTokenKind.Identifier:
            {
                state.Advance();
                if (state.Current.Kind != RuleTokenKind.OpenParen)
                {
                    return new RuleAttributeNode(token.Text, token.Position);
                }

                var arguments = ParseList(state) as RuleListNode;
                return new RuleFunctionNode(token.Text, arguments!.Items, token.Position);
            }

            case RuleTokenKind.Operator when token.IsOperator("<") || token.IsOperator(">"):
                throw new RuleSyntaxException($"Missing value before '{token.Text}'.", token.Position);

            default:
                throw new RuleSyntaxException(
                    token.Kind == RuleTokenKind.End
                        ? "Unexpected end of condition."
                        : $"Unexpected '{token.Text}'.",
                    token.Position);
        }
    }

    private static void Expect(ParserState state, Func<RuleToken, bool> predicate, string expected)
    {
        if (!predicate(state.Current))
        {
            throw new RuleSyntaxException(
                $"Expected '{expected}' but found "
                + (state.Current.Kind == RuleTokenKind.End ? "end of condition." : $"'{state.Current.Text}'."),
                state.Current.Position);
        }

        state.Advance();
    }

    private sealed class ParserState
    {
        private readonly IReadOnlyList<RuleToken> _tokens;
        private int _index;

        public ParserState(IReadOnlyList<RuleToken> tokens) => _tokens = tokens;

        public RuleToken Current => _tokens[_index];

        public void Advance()
        {
            if (_index < _tokens.Count - 1)
            {
                _index++;
            }
        }
    }
}
