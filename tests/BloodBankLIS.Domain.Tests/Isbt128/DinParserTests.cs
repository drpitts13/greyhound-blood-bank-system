using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Isbt128.Parsing;

namespace BloodBankLIS.Domain.Tests.Isbt128;

public class DinParserTests
{
    private readonly IDinCheckCharacterValidator _check = new PlaceholderDinCheckCharacterValidator();

    [Fact]
    public void ScannerDin_ParsesThirteenCharDinAndFlags()
    {
        var result = DinParser.Parse("=G12341765432100");
        Assert.True(result.Success);
        Assert.Equal("G123417654321", result.Value!.Din);
        Assert.Equal("00", result.Value.Flags);
        Assert.Equal("G1234", result.Value.Fin);
        Assert.Equal("17", result.Value.NominalYear);
        Assert.Equal("654321", result.Value.DonationSequence);
        Assert.True(result.Value.FromScanner);
    }

    [Fact]
    public void ScannerDin_PreservesCharacterCase()
    {
        var result = DinParser.Parse("=g12341765432100");
        Assert.True(result.Success);
        Assert.Equal("g123417654321", result.Value!.Din);
    }

    [Fact]
    public void ScannerDin_InvalidLength_Fails()
    {
        var result = DinParser.Parse("=G12341765432");
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == IsbtErrorCodes.InvalidDinLength);
    }

    [Fact]
    public void HumanReadable_WithSpaces_Parses()
    {
        var result = DinParser.Parse("G1234 17 654321");
        Assert.True(result.Success);
        Assert.Equal("G123417654321", result.Value!.Din);
        Assert.False(result.Value.FromScanner);
    }

    [Fact]
    public void KeyboardCheck_Mismatch_Fails()
    {
        var din = "G123417654321";
        var wrong = 'Z';
        if (_check.IsValid(din, wrong))
            wrong = 'Y';

        var result = DinParser.ParseStructured("G1234", "17", "654321", wrong.ToString(), _check, requireKeyboardCheck: true);
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == IsbtErrorCodes.DinCheckMismatch);
    }

    [Fact]
    public void KeyboardCheck_Match_Passes()
    {
        var din = "G123417654321";
        var check = _check.ComputeCheckCharacter(din);
        var result = DinParser.ParseStructured("G1234", "17", "654321", check.ToString(), _check, requireKeyboardCheck: true);
        Assert.True(result.Success);
        Assert.Equal(check.ToString(), result.Value!.KeyboardCheck);
    }

    [Fact]
    public void Sanitizer_RemovesCrLfAndStxEtx()
    {
        var sanitized = ScannerInputSanitizer.Sanitize("\u0002=G12341765432100\r\n\u0003");
        Assert.Equal("=G12341765432100", sanitized.Sanitized);
        Assert.Contains('\u0002', sanitized.Original);
    }

    [Fact]
    public void CompoundPayload_SplitsFourQuadrants()
    {
        var payload = "=G12341765432100=%DEMO=<E0206000=>2250200";
        var segments = CompoundIsbtPayloadSplitter.Split(payload);
        Assert.Equal(4, segments.Count);
    }
}
