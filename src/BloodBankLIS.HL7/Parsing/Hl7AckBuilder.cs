namespace BloodBankLIS.HL7.Parsing;

/// <summary>HL7 acknowledgement codes (MSA-1).</summary>
public static class AckCode
{
    public const string Accept = "AA";
    public const string ApplicationError = "AE";
    public const string Reject = "AR";
}

/// <summary>
/// Builds ACK/NAK responses. The acknowledgement echoes the inbound MSH-10 control id
/// in MSA-2 and swaps the sending/receiving application + facility, per
/// docs/hl7-design.md section 3.
/// </summary>
public static class Hl7AckBuilder
{
    public static string BuildAck(Hl7Message inbound, string ackCode, string? textMessage, string ackControlId, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(inbound);

        var builder = new Hl7MessageBuilder(inbound.Encoding)
            .AppendMsh(
                sendingApp: inbound.Get("MSH-5"),
                sendingFacility: inbound.Get("MSH-6"),
                receivingApp: inbound.Get("MSH-3"),
                receivingFacility: inbound.Get("MSH-4"),
                messageDateTimeUtc: nowUtc,
                messageType: "ACK",
                controlId: ackControlId,
                processingId: string.IsNullOrEmpty(inbound.Get("MSH-11")) ? "P" : inbound.Get("MSH-11"),
                version: string.IsNullOrEmpty(inbound.Get("MSH-12")) ? "2.5" : inbound.Get("MSH-12"));

        builder.AppendSegment("MSA", ackCode, inbound.MessageControlId, builder.Field(textMessage ?? string.Empty));
        return builder.Build();
    }

    /// <summary>
    /// Builds a NAK for a message that could not even be parsed (no inbound structure
    /// to echo). Uses default encoding and a best-effort control id.
    /// </summary>
    public static string BuildParseNak(string? inboundControlId, string textMessage, string ackControlId, DateTime nowUtc)
    {
        var builder = new Hl7MessageBuilder()
            .AppendMsh("BloodBankLIS", "BBLIS", "UNKNOWN", "UNKNOWN", nowUtc, "ACK", ackControlId);
        builder.AppendSegment("MSA", AckCode.Reject, inboundControlId ?? string.Empty, builder.Field(textMessage));
        return builder.Build();
    }
}
