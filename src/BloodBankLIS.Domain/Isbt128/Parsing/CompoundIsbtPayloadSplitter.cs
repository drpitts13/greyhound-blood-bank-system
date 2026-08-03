using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Isbt128.Parsing;

/// <summary>
/// Splits concatenated scanner input or Data Matrix multi-structure payloads
/// into individual ISBT structures using known data identifiers.
/// </summary>
public static class CompoundIsbtPayloadSplitter
{
    public sealed record Segment(string Value, IsbtDataStructureKind Kind);

    public static IReadOnlyList<Segment> Split(string? input, ScannerInputSanitizer.Options? options = null)
    {
        var sanitized = ScannerInputSanitizer.Sanitize(input, options).Sanitized;
        if (string.IsNullOrEmpty(sanitized))
            return Array.Empty<Segment>();

        // Single structure?
        var kind = IsbtDataStructureRegistry.Classify(sanitized);
        if (kind != IsbtDataStructureKind.Unknown && !ContainsAdditionalIdentifier(sanitized, kind))
            return new[] { new Segment(sanitized, kind) };

        var segments = new List<Segment>();
        var prefixes = IsbtDataStructureRegistry.All
            .Select(e => e.Prefix)
            .OrderByDescending(p => p.Length)
            .ToArray();

        var i = 0;
        while (i < sanitized.Length)
        {
            string? matched = null;
            foreach (var prefix in prefixes)
            {
                if (i + prefix.Length <= sanitized.Length
                    && sanitized.AsSpan(i).StartsWith(prefix, StringComparison.Ordinal))
                {
                    matched = prefix;
                    break;
                }
            }

            if (matched is null)
            {
                // Skip unknown leading junk until next identifier.
                i++;
                continue;
            }

            var start = i;
            i += matched.Length;
            while (i < sanitized.Length)
            {
                var atIdentifier = false;
                foreach (var prefix in prefixes)
                {
                    if (i + prefix.Length <= sanitized.Length
                        && sanitized.AsSpan(i).StartsWith(prefix, StringComparison.Ordinal))
                    {
                        atIdentifier = true;
                        break;
                    }
                }

                if (atIdentifier)
                    break;
                i++;
            }

            var value = sanitized[start..i];
            var segKind = IsbtDataStructureRegistry.Classify(value);
            if (segKind != IsbtDataStructureKind.Unknown)
                segments.Add(new Segment(value, segKind));
        }

        return segments;
    }

    private static bool ContainsAdditionalIdentifier(string sanitized, IsbtDataStructureKind firstKind)
    {
        var prefix = IsbtDataStructureRegistry.GetPrefix(firstKind);
        if (prefix is null)
            return false;

        var rest = sanitized[prefix.Length..];
        foreach (var (p, _) in IsbtDataStructureRegistry.All)
        {
            if (rest.Contains(p, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
