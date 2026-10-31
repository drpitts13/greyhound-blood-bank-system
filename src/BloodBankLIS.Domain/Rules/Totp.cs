using System.Security.Cryptography;
using System.Text;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// RFC 6238 time-based one-time passwords (HMAC-SHA1, 30-second step, 6 digits), the
/// format authenticator apps use. A code is accepted one step either side of now to allow
/// for clock drift, and never for a step at or before the last one used (no replay).
/// </summary>
public static class Totp
{
    public const int StepSeconds = 30;
    public const int Digits = 6;
    public const int AllowedDriftSteps = 1;
    public const int SecretBytes = 20;

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static long StepAt(DateTime utc) =>
        (long)Math.Floor((utc - DateTime.UnixEpoch).TotalSeconds / StepSeconds);

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(SecretBytes);

    public static string Code(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(step & 0xFF);
            step >>= 8;
        }

        var hash = HMACSHA1.HashData(secret, counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The matched step, or null when the code is wrong, malformed, or replayed.</summary>
    public static long? Verify(byte[] secret, string? code, DateTime nowUtc, long? lastUsedStep)
    {
        var digits = new string((code ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (digits.Length != Digits || !digits.All(char.IsAsciiDigit))
        {
            return null;
        }

        var now = StepAt(nowUtc);
        for (var drift = -AllowedDriftSteps; drift <= AllowedDriftSteps; drift++)
        {
            var step = now + drift;
            if (lastUsedStep is long last && step <= last)
            {
                continue;
            }

            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(Code(secret, step)),
                    Encoding.ASCII.GetBytes(digits)))
            {
                return step;
            }
        }

        return null;
    }

    public static string ToBase32(byte[] data)
    {
        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                result.Append(Base32Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            result.Append(Base32Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return result.ToString();
    }

    public static byte[] FromBase32(string text)
    {
        var clean = text.Trim().TrimEnd('=').Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        var output = new List<byte>(clean.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in clean)
        {
            var value = Base32Alphabet.IndexOf(c);
            if (value < 0)
            {
                throw new FormatException($"'{c}' is not a base32 character.");
            }

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return output.ToArray();
    }

    /// <summary>otpauth:// URI an authenticator app can read.</summary>
    public static string ProvisioningUri(string issuer, string account, byte[] secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}" +
        $"?secret={ToBase32(secret)}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
}
