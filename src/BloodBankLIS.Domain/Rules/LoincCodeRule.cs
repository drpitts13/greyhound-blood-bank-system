namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A LOINC code as the catalog stores it: 1 to 7 digits, a hyphen, and the Mod-10 check digit (for example 882-1).
/// The rule only checks the form; which LOINC code a test maps to is the interface team's decision.
/// </summary>
public static class LoincCodeRule
{
    public const string Code = "TESTDEF.LOINC";

    /// <summary>"882-1" and similar; null, blank, or a wrong check digit is not valid.</summary>
    public static bool IsValid(string? loinc)
    {
        var text = loinc?.Trim() ?? string.Empty;
        var hyphen = text.IndexOf('-');
        if (hyphen < 1 || hyphen > 7 || hyphen != text.Length - 2)
        {
            return false;
        }

        var body = text[..hyphen];
        return body.All(char.IsAsciiDigit) && char.IsAsciiDigit(text[^1]) && CheckDigit(body) == text[^1] - '0';
    }

    /// <summary>The Mod-10 (Luhn) check digit LOINC uses: from the right, every other digit doubled, digits summed.</summary>
    public static int CheckDigit(string digits)
    {
        var sum = 0;
        var doubleIt = true;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }

            sum += d;
            doubleIt = !doubleIt;
        }

        return (10 - sum % 10) % 10;
    }
}
