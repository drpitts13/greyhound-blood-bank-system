namespace BloodBankLIS.Astm.Framing;

/// <summary>LIS1-A transmission control characters.</summary>
public static class AstmControlChars
{
    public const byte Enq = 0x05;
    public const byte Ack = 0x06;
    public const byte Nak = 0x15;
    public const byte Eot = 0x04;
    public const byte Stx = 0x02;
    public const byte Etb = 0x17;
    public const byte Etx = 0x03;
    public const byte Cr = 0x0D;
    public const byte Lf = 0x0A;

    public const int MaxFrameTextLength = 240;
    public const int MaxRetries = 6;
}
