using System.Text;
using BloodBankLIS.HL7.Mllp;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Tests;

public class Hl7AckBuilderTests
{
    private const string Inbound =
        "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ADT^A01|MSG00001|P|2.5\rPID|1||MRN1";

    [Fact]
    public void BuildAck_EchoesControlId_AndSwapsApplications()
    {
        var inbound = Hl7Parser.Parse(Inbound);
        var ack = Hl7AckBuilder.BuildAck(inbound, AckCode.Accept, "ok", "ACK001", new DateTime(2026, 5, 30, 12, 0, 5, DateTimeKind.Utc));

        var parsed = Hl7Parser.Parse(ack);
        Assert.Equal("ACK", parsed.MessageType);
        Assert.Equal("AA", parsed.Get("MSA-1"));
        Assert.Equal("MSG00001", parsed.Get("MSA-2"));
        // Sending/receiving applications are swapped relative to the inbound message.
        Assert.Equal("BBLIS", parsed.Get("MSH-3"));
        Assert.Equal("EHR", parsed.Get("MSH-5"));
    }

    [Fact]
    public void BuildParseNak_ProducesRejectWithoutInboundStructure()
    {
        var nak = Hl7AckBuilder.BuildParseNak(null, "bad message", "ACK002", DateTime.UtcNow);
        var parsed = Hl7Parser.Parse(nak);

        Assert.Equal("AR", parsed.Get("MSA-1"));
    }
}

public class MllpFramingTests
{
    [Fact]
    public void Wrap_AddsStartEndAndCarriageReturn()
    {
        var framed = MllpFraming.Wrap("HELLO");

        Assert.Equal(MllpFraming.StartBlock, framed[0]);
        Assert.Equal(MllpFraming.EndBlock, framed[^2]);
        Assert.Equal(MllpFraming.CarriageReturn, framed[^1]);
        Assert.Equal("HELLO", Encoding.UTF8.GetString(framed, 1, framed.Length - 3));
    }

    [Fact]
    public void Extract_ReturnsCompleteMessages_AndConsumedCount()
    {
        var buffer = MllpFraming.Wrap("MSG1").Concat(MllpFraming.Wrap("MSG2")).ToArray();

        var messages = MllpFraming.Extract(buffer, out var consumed);

        Assert.Equal(new[] { "MSG1", "MSG2" }, messages);
        Assert.Equal(buffer.Length, consumed);
    }

    [Fact]
    public void Extract_LeavesPartialTrailingFrameUnconsumed()
    {
        var complete = MllpFraming.Wrap("DONE");
        var partial = MllpFraming.Wrap("PARTIAL");
        var buffer = complete.Concat(partial.Take(partial.Length - 1)).ToArray(); // drop trailing CR

        var messages = MllpFraming.Extract(buffer, out var consumed);

        Assert.Equal(new[] { "DONE" }, messages);
        Assert.Equal(complete.Length, consumed);
    }
}
