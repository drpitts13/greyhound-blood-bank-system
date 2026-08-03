using System.Globalization;
using System.Text;

namespace BloodBankLIS.Domain.Rules.Engine;

public enum RuleTokenKind
{
    Identifier = 0,
    Text = 1,
    Number = 2,
    Keyword = 3,
    Operator = 4,
    OpenParen = 5,
    CloseParen = 6,
    Comma = 7,
    Semicolon = 8,
    End = 9
}

public sealed record RuleToken(RuleTokenKind Kind, string Text, int Position)
{
    public bool IsKeyword(string keyword) =>
        Kind == RuleTokenKind.Keyword && string.Equals(Text, keyword, StringComparison.OrdinalIgnoreCase);

    public bool IsOperator(string op) =>
        Kind == RuleTokenKind.Operator && string.Equals(Text, op, StringComparison.Ordinal);
}

/// <summary>Raised when rule text cannot be tokenized or parsed. Carries the character offset.</summary>
public sealed class RuleSyntaxException : Exception
{
    public RuleSyntaxException(string message, int position)
        : base($"{message} (position {position})")
    {
        Position = position;
    }

    public int Position { get; }
}

/// <summary>
/// Tokenizer for the rule expression language. Dotted attribute paths such as
/// <c>patient.ageDays</c> are emitted as a single identifier token.
/// </summary>
public static class RuleLexer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND", "OR", "NOT", "IN", "CONTAINS", "IS", "NULL", "TRUE", "FALSE"
    };

    public static IReadOnlyList<RuleToken> Tokenize(string? input)
    {
        var tokens = new List<RuleToken>();
        var text = input ?? string.Empty;
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            switch (c)
            {
                case '(':
                    tokens.Add(new RuleToken(RuleTokenKind.OpenParen, "(", i++));
                    continue;
                case ')':
                    tokens.Add(new RuleToken(RuleTokenKind.CloseParen, ")", i++));
                    continue;
                case ',':
                    tokens.Add(new RuleToken(RuleTokenKind.Comma, ",", i++));
                    continue;
                case ';':
                    tokens.Add(new RuleToken(RuleTokenKind.Semicolon, ";", i++));
                    continue;
                case '\'':
                case '"':
                    tokens.Add(ReadText(text, ref i));
                    continue;
            }

            if (char.IsDigit(c))
            {
                tokens.Add(ReadNumber(text, ref i));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                tokens.Add(ReadIdentifier(text, ref i));
                continue;
            }

            var op = ReadOperator(text, ref i);
            tokens.Add(op);
        }

        tokens.Add(new RuleToken(RuleTokenKind.End, string.Empty, text.Length));
        return tokens;
    }

    private static RuleToken ReadText(string text, ref int i)
    {
        var start = i;
        var quote = text[i++];
        var builder = new StringBuilder();

        while (true)
        {
            if (i >= text.Length)
            {
                throw new RuleSyntaxException("Unterminated text literal.", start);
            }

            var c = text[i];
            if (c == quote)
            {
                // Doubled quote is an escaped quote, e.g. 'it''s'.
                if (i + 1 < text.Length && text[i + 1] == quote)
                {
                    builder.Append(quote);
                    i += 2;
                    continue;
                }

                i++;
                break;
            }

            builder.Append(c);
            i++;
        }

        return new RuleToken(RuleTokenKind.Text, builder.ToString(), start);
    }

    private static RuleToken ReadNumber(string text, ref int i)
    {
        var start = i;
        var seenDot = false;

        while (i < text.Length && (char.IsDigit(text[i]) || (text[i] == '.' && !seenDot)))
        {
            if (text[i] == '.')
            {
                // A trailing dot is not part of the number.
                if (i + 1 >= text.Length || !char.IsDigit(text[i + 1]))
                {
                    break;
                }

                seenDot = true;
            }

            i++;
        }

        var raw = text[start..i];
        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            throw new RuleSyntaxException($"'{raw}' is not a valid number.", start);
        }

        return new RuleToken(RuleTokenKind.Number, raw, start);
    }

    private static RuleToken ReadIdentifier(string text, ref int i)
    {
        var start = i;

        while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_' || text[i] == '.'))
        {
            // Only consume a dot that is followed by another identifier character.
            if (text[i] == '.' && (i + 1 >= text.Length || !(char.IsLetter(text[i + 1]) || text[i + 1] == '_')))
            {
                break;
            }

            i++;
        }

        var raw = text[start..i];
        return Keywords.Contains(raw)
            ? new RuleToken(RuleTokenKind.Keyword, raw.ToUpperInvariant(), start)
            : new RuleToken(RuleTokenKind.Identifier, raw, start);
    }

    private static RuleToken ReadOperator(string text, ref int i)
    {
        var start = i;

        if (i + 1 < text.Length)
        {
            var pair = text.Substring(i, 2);
            switch (pair)
            {
                case "==":
                    i += 2;
                    return new RuleToken(RuleTokenKind.Operator, "=", start);
                case "!=":
                case "<>":
                    i += 2;
                    return new RuleToken(RuleTokenKind.Operator, "!=", start);
                case "<=":
                case ">=":
                    i += 2;
                    return new RuleToken(RuleTokenKind.Operator, pair, start);
                case "&&":
                    i += 2;
                    return new RuleToken(RuleTokenKind.Keyword, "AND", start);
                case "||":
                    i += 2;
                    return new RuleToken(RuleTokenKind.Keyword, "OR", start);
            }
        }

        var single = text[i];
        switch (single)
        {
            case '=':
            case '<':
            case '>':
                i++;
                return new RuleToken(RuleTokenKind.Operator, single.ToString(), start);
            case '!':
                i++;
                return new RuleToken(RuleTokenKind.Keyword, "NOT", start);
        }

        throw new RuleSyntaxException($"Unexpected character '{single}'.", start);
    }
}
