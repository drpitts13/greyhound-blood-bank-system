using System.Text;

namespace BloodBankLIS.HL7.Mllp;

/// <summary>
/// Minimal Lower Layer Protocol framing for HL7 over TCP: a message is wrapped as
/// &lt;VT&gt; message &lt;FS&gt;&lt;CR&gt; (0x0B ... 0x1C 0x0D). Pure byte helpers;
/// the actual socket lives in the Api hosted service (see docs/hl7-design.md 4).
/// </summary>
public static class MllpFraming
{
    public const byte StartBlock = 0x0B;
    public const byte EndBlock = 0x1C;
    public const byte CarriageReturn = 0x0D;

    public static byte[] Wrap(string message, Encoding? encoding = null)
    {
        var payload = (encoding ?? Encoding.UTF8).GetBytes(message);
        var framed = new byte[payload.Length + 3];
        framed[0] = StartBlock;
        Array.Copy(payload, 0, framed, 1, payload.Length);
        framed[^2] = EndBlock;
        framed[^1] = CarriageReturn;
        return framed;
    }

    /// <summary>
    /// Extracts complete framed messages from a receive buffer, returning the decoded
    /// messages and the number of bytes consumed (so the caller can keep any partial
    /// trailing frame for the next read).
    /// </summary>
    public static IReadOnlyList<string> Extract(ReadOnlySpan<byte> buffer, out int consumedBytes, Encoding? encoding = null)
    {
        var enc = encoding ?? Encoding.UTF8;
        var messages = new List<string>();
        consumedBytes = 0;
        var index = 0;

        while (index < buffer.Length)
        {
            if (buffer[index] != StartBlock)
            {
                index++;
                continue;
            }

            var end = -1;
            for (var i = index + 1; i < buffer.Length - 1; i++)
            {
                if (buffer[i] == EndBlock && buffer[i + 1] == CarriageReturn)
                {
                    end = i;
                    break;
                }
            }

            if (end < 0)
            {
                break; // incomplete frame; leave for next read
            }

            var payload = buffer.Slice(index + 1, end - index - 1);
            messages.Add(enc.GetString(payload));
            index = end + 2;
            consumedBytes = index;
        }

        return messages;
    }
}
