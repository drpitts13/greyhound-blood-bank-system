using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Tests;

public class Hl7ParserTests
{
    private const string Adt =
        "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ADT^A01|MSG00001|P|2.5\r" +
        "PID|1||MRN12345^^^HOSP^MR||Doe^John^Q||19800115|M\r" +
        "PV1|1|I|WEST^101^A||||||||||||||||V00042";

    [Fact]
    public void Parse_ReadsMessageTypeTriggerAndControlId()
    {
        var message = Hl7Parser.Parse(Adt);

        Assert.Equal("ADT", message.MessageType);
        Assert.Equal("A01", message.TriggerEvent);
        Assert.Equal("MSG00001", message.MessageControlId);
    }

    [Fact]
    public void Parse_ReadsComponentsViaLocationPath()
    {
        var message = Hl7Parser.Parse(Adt);

        Assert.Equal("MRN12345", message.Get("PID-3-1"));
        Assert.Equal("Doe", message.Get("PID-5-1"));
        Assert.Equal("John", message.Get("PID-5-2"));
        Assert.Equal("Q", message.Get("PID-5-3"));
        Assert.Equal("19800115", message.Get("PID-7"));
        Assert.Equal("M", message.Get("PID-8"));
        Assert.Equal("V00042", message.Get("PV1-19"));
    }

    [Fact]
    public void Get_ReturnsEmptyStringForMissingFields_NotNullOrThrow()
    {
        var message = Hl7Parser.Parse(Adt);

        Assert.Equal(string.Empty, message.Get("ZZZ-9-9"));
        Assert.Equal(string.Empty, message.Get("PID-99"));
        Assert.Equal(string.Empty, message.Get("PID-5-9"));
    }

    [Fact]
    public void Parse_HonorsCustomEncodingCharactersFromHeader()
    {
        // Field separator '#', component '@'.
        var raw = "MSH#@~\\&#EHR#HOSP#BBLIS#LAB#20260530120000##ADT@A01#CID9#P#2.5\rPID#1##MRN9@@@HOSP";
        var message = Hl7Parser.Parse(raw);

        Assert.Equal('#', message.Encoding.FieldSeparator);
        Assert.Equal('@', message.Encoding.ComponentSeparator);
        Assert.Equal("ADT", message.MessageType);
        Assert.Equal("MRN9", message.Get("PID-3-1"));
    }

    [Fact]
    public void Parse_FirstRepetitionIsReturned()
    {
        var raw = Adt.Replace("MRN12345^^^HOSP^MR", "MRN12345^^^HOSP^MR~ALT999^^^OTHER^MR");
        var message = Hl7Parser.Parse(raw);

        Assert.Equal("MRN12345", message.Get("PID-3-1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("PID|1|garbage")]
    public void Parse_InvalidMessages_Throw(string raw)
    {
        Assert.Throws<Hl7ParseException>(() => Hl7Parser.Parse(raw));
    }

    [Fact]
    public void TryParse_ReturnsFalseWithError_ForNonMshStart()
    {
        var ok = Hl7Parser.TryParse("PID|1", out var message, out var error);

        Assert.False(ok);
        Assert.Null(message);
        Assert.NotNull(error);
    }
}
