using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Tests;

public class Hl7EncodingTests
{
    private readonly Hl7Encoding _enc = Hl7Encoding.Default;

    [Fact]
    public void EncodingCharacters_AreStandardSet()
    {
        Assert.Equal("^~\\&", _enc.EncodingCharacters);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a|b", "a\\F\\b")]
    [InlineData("a^b", "a\\S\\b")]
    [InlineData("a~b", "a\\R\\b")]
    [InlineData("a&b", "a\\T\\b")]
    [InlineData("a\\b", "a\\E\\b")]
    public void Escape_EncodesSeparators(string input, string expected)
    {
        Assert.Equal(expected, _enc.Escape(input));
    }

    [Theory]
    [InlineData("Smith & Sons")]
    [InlineData("a|b^c~d&e\\f")]
    [InlineData("no specials here")]
    public void EscapeThenUnescape_RoundTrips(string original)
    {
        var roundTripped = _enc.Unescape(_enc.Escape(original));
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void FromMshHeader_FallsBackToDefaultsWhenAbsent()
    {
        var encoding = Hl7Encoding.FromMshHeader("not an hl7 message");
        Assert.Equal('|', encoding.FieldSeparator);
        Assert.Equal('^', encoding.ComponentSeparator);
    }
}
