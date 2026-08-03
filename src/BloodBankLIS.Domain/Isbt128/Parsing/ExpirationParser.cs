namespace BloodBankLIS.Domain.Isbt128.Parsing;

/// <summary>
/// Parses =>cyyjjj (date) and &>cyyjjjhhmm (date/time).
/// INSTITUTIONAL_POLICY_REVIEW: when only a date is encoded, operational time defaults
/// to end-of-day (commonly 23:59) in the facility timezone; source flag preserves
/// that the label did not contain an explicit time.
/// </summary>
public static class ExpirationParser
{
    public sealed record Policy(
        string TimezoneId,
        TimeOnly DateOnlyDefaultTime,
        int CenturyBaseYear);

    public static Policy DefaultPolicy { get; } = new(
        TimezoneId: "UTC",
        DateOnlyDefaultTime: new TimeOnly(23, 59),
        CenturyBaseYear: 2000);

    public static ParseOutcome<ExpirationParseResult> Parse(
        string? input,
        Policy? policy = null,
        ScannerInputSanitizer.Options? sanitizeOptions = null)
    {
        policy ??= DefaultPolicy;
        var sanitizedResult = ScannerInputSanitizer.Sanitize(input, sanitizeOptions);
        var sanitized = sanitizedResult.Sanitized;

        bool hasTime;
        string payload;
        if (sanitized.StartsWith("&>", StringComparison.Ordinal))
        {
            hasTime = true;
            payload = sanitized[2..];
        }
        else if (sanitized.StartsWith("=>", StringComparison.Ordinal))
        {
            hasTime = false;
            payload = sanitized[2..];
        }
        else
        {
            return ParseOutcome<ExpirationParseResult>.Fail(
                IsbtErrorCodes.UnsupportedDataStructure,
                "Expiration must start with '=>' or '&>'.");
        }

        // ICCBBA_VALIDATION_REQUIRED: confirm encoded lengths against current documentation.
        // Conceptual forms: =>cyyjjj (6) and &>cyyjjjhhmm (10).
        var expectedLen = hasTime ? 10 : 6;
        if (payload.Length != expectedLen || !payload.All(char.IsDigit))
        {
            return ParseOutcome<ExpirationParseResult>.Fail(
                IsbtErrorCodes.InvalidExpiration,
                hasTime
                    ? "Expiration date/time payload must be cyyjjjhhmm (10 digits)."
                    : "Expiration date payload must be cyyjjj (6 digits).");
        }

        var century = payload[0];
        var yy = int.Parse(payload[1..3]);
        var jjj = int.Parse(payload[3..6]);
        int? hour = hasTime ? int.Parse(payload[6..8]) : null;
        int? minute = hasTime ? int.Parse(payload[8..10]) : null;

        if (century is not ('0' or '1' or '2'))
        {
            return ParseOutcome<ExpirationParseResult>.Fail(
                IsbtErrorCodes.InvalidExpiration,
                "Century indicator must be 0, 1, or 2. ICCBBA_VALIDATION_REQUIRED.");
        }

        var year = ResolveYear(century, yy, policy.CenturyBaseYear);
        if (jjj < 1 || jjj > (DateTime.IsLeapYear(year) ? 366 : 365))
        {
            return ParseOutcome<ExpirationParseResult>.Fail(
                IsbtErrorCodes.InvalidExpiration,
                $"Ordinal day {jjj} is invalid for year {year}.");
        }

        if (hasTime)
        {
            if (hour is < 0 or > 23 || minute is < 0 or > 59)
            {
                return ParseOutcome<ExpirationParseResult>.Fail(
                    IsbtErrorCodes.InvalidExpiration,
                    "Hour must be 00–23 and minute 00–59.");
            }
        }

        var date = new DateOnly(year, 1, 1).AddDays(jjj - 1);
        var time = hasTime
            ? new TimeOnly(hour!.Value, minute!.Value)
            : policy.DateOnlyDefaultTime;
        var local = date.ToDateTime(time);

        return ParseOutcome<ExpirationParseResult>.Ok(new ExpirationParseResult(
            ExpirationEncoded: payload,
            ExpirationLocal: local,
            ExpirationTimezone: policy.TimezoneId,
            ExpirationHasExplicitTime: hasTime,
            CenturyIndicator: century.ToString(),
            Year: year,
            OrdinalDay: jjj,
            Hour: hour,
            Minute: minute,
            RawScan: sanitizedResult.Original,
            Sanitized: sanitized,
            FromScanner: true));
    }

    public static ParseOutcome<ExpirationParseResult> FromLocalDateTime(
        DateTime expirationLocal,
        bool hasExplicitTime,
        Policy? policy = null)
    {
        policy ??= DefaultPolicy;
        var year = expirationLocal.Year;
        var ordinal = expirationLocal.DayOfYear;
        var century = year >= 2000 ? '2' : year >= 1900 ? '1' : '0';
        var yy = year % 100;
        var encoded = hasExplicitTime
            ? $"{century}{yy:D2}{ordinal:D3}{expirationLocal.Hour:D2}{expirationLocal.Minute:D2}"
            : $"{century}{yy:D2}{ordinal:D3}";

        return ParseOutcome<ExpirationParseResult>.Ok(new ExpirationParseResult(
            ExpirationEncoded: encoded,
            ExpirationLocal: hasExplicitTime
                ? expirationLocal
                : new DateOnly(year, expirationLocal.Month, expirationLocal.Day)
                    .ToDateTime(policy.DateOnlyDefaultTime),
            ExpirationTimezone: policy.TimezoneId,
            ExpirationHasExplicitTime: hasExplicitTime,
            CenturyIndicator: century.ToString(),
            Year: year,
            OrdinalDay: ordinal,
            Hour: hasExplicitTime ? expirationLocal.Hour : null,
            Minute: hasExplicitTime ? expirationLocal.Minute : null,
            RawScan: null,
            Sanitized: (hasExplicitTime ? "&>" : "=>") + encoded,
            FromScanner: false));
    }

    private static int ResolveYear(char century, int yy, int centuryBaseYear)
    {
        // ICCBBA_VALIDATION_REQUIRED: confirm century mapping against current standard.
        return century switch
        {
            '0' => 1900 + yy,
            '1' => 1900 + yy,
            '2' => 2000 + yy,
            _ => centuryBaseYear + yy
        };
    }
}
