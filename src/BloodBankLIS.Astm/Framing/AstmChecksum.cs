using System.Text;

namespace BloodBankLIS.Astm.Framing;

/// <summary>
/// LIS1-A checksum: modulo-256 sum of the frame number, text, and ETB/ETX,
/// encoded as two uppercase hex digits.
/// </summary>
public static class AstmChecksum
{
    public static string Compute(char frameNumber, ReadOnlySpan<char> text, byte terminator)
    {
        var sum = (int)frameNumber;
        foreach (var c in text)
        {
            sum += c;
        }

        sum += terminator;
        return (sum % 256).ToString("X2");
    }

    public static bool TryValidate(char frameNumber, ReadOnlySpan<char> text, byte terminator, ReadOnlySpan<char> hex)
    {
        if (hex.Length != 2)
        {
            return false;
        }

        return string.Equals(Compute(frameNumber, text, terminator), hex.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public static byte[] EncodeAscii(string value) => Encoding.ASCII.GetBytes(value);
}
