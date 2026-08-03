namespace BloodBankLIS.Domain.Isbt128;

/// <summary>
/// Removes configurable transport characters from scanner input without altering
/// meaningful ISBT identifier characters or globally changing character case.
/// </summary>
public static class ScannerInputSanitizer
{
    public sealed record Options(
        IReadOnlyList<string>? Prefixes = null,
        IReadOnlyList<string>? Suffixes = null,
        bool StripAimIdentifiers = false);

    public sealed record Result(string Original, string Sanitized);

    public static Result Sanitize(string? value, Options? options = null)
    {
        var original = value ?? string.Empty;
        options ??= new Options();

        var chars = new List<char>(original.Length);
        foreach (var c in original)
        {
            if (c is '\u0002' or '\u0003' or '\r' or '\n' or '\t' or '\0')
                continue;
            chars.Add(c);
        }

        var sanitized = new string(chars.ToArray());

        if (options.StripAimIdentifiers && sanitized.Length >= 3 && sanitized[0] == ']' )
        {
            // AIM symbology identifier form: ]cm where c is symbology, m is modifier.
            sanitized = sanitized[3..];
        }

        if (options.Prefixes is { Count: > 0 })
        {
            foreach (var prefix in options.Prefixes.OrderByDescending(p => p.Length))
            {
                if (!string.IsNullOrEmpty(prefix) && sanitized.StartsWith(prefix, StringComparison.Ordinal))
                {
                    sanitized = sanitized[prefix.Length..];
                    break;
                }
            }
        }

        if (options.Suffixes is { Count: > 0 })
        {
            foreach (var suffix in options.Suffixes.OrderByDescending(s => s.Length))
            {
                if (!string.IsNullOrEmpty(suffix) && sanitized.EndsWith(suffix, StringComparison.Ordinal))
                {
                    sanitized = sanitized[..^suffix.Length];
                    break;
                }
            }
        }

        return new Result(original, sanitized);
    }
}
