using System.Text;

namespace BloodBankLIS.Astm.Framing;

public sealed record AstmFrame(int FrameNumber, string Text, bool IsLast);

/// <summary>Builds and parses LIS1-A intermediate/end frames.</summary>
public static class AstmFraming
{
    public static IReadOnlyList<AstmFrame> Split(string message)
    {
        var text = message.Replace("\n", "", StringComparison.Ordinal);
        if (!text.EndsWith('\r'))
        {
            text += "\r";
        }

        var frames = new List<AstmFrame>();
        var remaining = text.AsSpan();
        var fn = 1;
        while (remaining.Length > 0)
        {
            var take = Math.Min(AstmControlChars.MaxFrameTextLength, remaining.Length);
            var chunk = remaining[..take].ToString();
            remaining = remaining[take..];
            frames.Add(new AstmFrame(fn, chunk, remaining.Length == 0));
            fn = fn == 7 ? 0 : fn + 1;
        }

        return frames;
    }

    public static byte[] Encode(AstmFrame frame)
    {
        var terminator = frame.IsLast ? AstmControlChars.Etx : AstmControlChars.Etb;
        var fn = FrameDigit(frame.FrameNumber);
        var checksum = AstmChecksum.Compute(fn, frame.Text, terminator);
        var payload = new StringBuilder(frame.Text.Length + 8);
        payload.Append((char)AstmControlChars.Stx);
        payload.Append(fn);
        payload.Append(frame.Text);
        payload.Append((char)terminator);
        payload.Append(checksum);
        payload.Append((char)AstmControlChars.Cr);
        payload.Append((char)AstmControlChars.Lf);
        return Encoding.Latin1.GetBytes(payload.ToString());
    }

    public static bool TryExtract(List<byte> buffer, out AstmFrame? frame, out string? error)
    {
        frame = null;
        error = null;
        var stx = buffer.IndexOf(AstmControlChars.Stx);
        if (stx < 0)
        {
            return false;
        }

        if (stx > 0)
        {
            buffer.RemoveRange(0, stx);
        }

        if (buffer.Count < 7)
        {
            return false;
        }

        var end = -1;
        byte terminator = 0;
        for (var i = 2; i < buffer.Count - 3; i++)
        {
            if (buffer[i] is AstmControlChars.Etb or AstmControlChars.Etx
                && i + 4 < buffer.Count
                && buffer[i + 3] == AstmControlChars.Cr
                && buffer[i + 4] == AstmControlChars.Lf)
            {
                end = i;
                terminator = buffer[i];
                break;
            }
        }

        if (end < 0)
        {
            return false;
        }

        var fnChar = (char)buffer[1];
        if (fnChar is < '0' or > '7')
        {
            error = "Invalid frame number.";
            buffer.RemoveRange(0, end + 5);
            return true;
        }

        var textBytes = buffer.GetRange(2, end - 2).ToArray();
        var text = Encoding.Latin1.GetString(textBytes);
        var checksum = Encoding.Latin1.GetString(buffer.GetRange(end + 1, 2).ToArray());
        buffer.RemoveRange(0, end + 5);

        if (!AstmChecksum.TryValidate(fnChar, text, terminator, checksum))
        {
            error = "Checksum mismatch.";
            return true;
        }

        frame = new AstmFrame(fnChar - '0', text, terminator == AstmControlChars.Etx);
        return true;
    }

    public static char FrameDigit(int frameNumber)
    {
        var n = frameNumber % 8;
        if (n < 0)
        {
            n += 8;
        }

        return (char)('0' + n);
    }
}
