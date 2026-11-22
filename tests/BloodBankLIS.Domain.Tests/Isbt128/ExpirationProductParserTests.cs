using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Isbt128.Parsing;

namespace BloodBankLIS.Domain.Tests.Isbt128;

public class ExpirationProductParserTests
{
    private static IReadOnlyDictionary<string, ProductParser.LookupRow> ProductLookup() =>
        new Dictionary<string, ProductParser.LookupRow>
        {
            ["E0206"] = new(
                "E0206",
                "PLACEHOLDER RBC",
                "RedBloodCells",
                null,
                Array.Empty<string>(),
                null,
                RequiresExtendedDivision: false,
                null,
                null,
                "PLACEHOLDER")
        };

    private static IReadOnlyDictionary<string, AboRhdParser.LookupRow> AboLookup() =>
        new Dictionary<string, AboRhdParser.LookupRow>
        {
            ["DEMO"] = new("DEMO", AboGroup.O, RhType.Positive, "Volunteer", null, null, null, null, "PLACEHOLDER")
        };

    [Fact]
    public void Product_ParsesEightCharData()
    {
        var result = ProductParser.ParseScanner("=<E0206000", ProductLookup());
        Assert.True(result.Success);
        Assert.Equal("E0206", result.Value!.ProductDescriptionCode);
        Assert.Equal("0", result.Value.CollectionTypeCode);
        Assert.Equal("00", result.Value.DivisionCode);
        Assert.Equal("E0206000", result.Value.ProductCodeData);
    }

    [Fact]
    public void Product_Retired_BlockedForNewManufacture()
    {
        var lookup = new Dictionary<string, ProductParser.LookupRow>
        {
            ["E0206"] = new("E0206", "Retired", "RedBloodCells", null, Array.Empty<string>(), null, false,
                null, new DateOnly(2020, 1, 1), "PLACEHOLDER")
        };
        var result = ProductParser.ParseScanner("=<E0206000", lookup, allowRetiredForExistingInventory: false, isNewManufactureOrRelabel: true);
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == IsbtErrorCodes.RetiredProductNotAllowed);
    }

    [Fact]
    public void Abo_UnknownCode_Fails()
    {
        var result = AboRhdParser.ParseScanner("=%XXXX", AboLookup());
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == IsbtErrorCodes.UnknownAboRhdCode);
    }

    [Fact]
    public void Abo_KnownPlaceholder_Decodes()
    {
        var result = AboRhdParser.ParseScanner("=%DEMO", AboLookup());
        Assert.True(result.Success);
        Assert.Equal(AboGroup.O, result.Value!.Abo);
        Assert.Equal(RhType.Positive, result.Value.RhD);
    }

    [Fact]
    public void Expiration_DateOnly_UsesPolicyTime()
    {
        // c=0 (2000s, OCD-092), yy=25, jjj=200 → =>025200
        var result = ExpirationParser.Parse("=>025200");
        Assert.True(result.Success, string.Join(";", result.Errors.Select(e => e.Message)));
        Assert.False(result.Value!.ExpirationHasExplicitTime);
        Assert.Equal(23, result.Value.ExpirationLocal.Hour);
        Assert.Equal(59, result.Value.ExpirationLocal.Minute);
    }

    [Fact]
    public void Expiration_DateTime_ParsesHourMinute()
    {
        // c=0, yy=25, jjj=200, hh=14, mm=30 → &>0252001430 (10 digits)
        var result = ExpirationParser.Parse("&>0252001430");
        Assert.True(result.Success, string.Join(";", result.Errors.Select(e => e.Message)));
        Assert.True(result.Value!.ExpirationHasExplicitTime);
        Assert.Equal(14, result.Value.Hour);
        Assert.Equal(30, result.Value.Minute);
    }

    [Fact]
    public void Expiration_InvalidOrdinal_Fails()
    {
        var result = ExpirationParser.Parse("=>025400");
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == IsbtErrorCodes.InvalidExpiration);
    }

    [Fact]
    public void Expiration_LeapYearDay366_Ok()
    {
        var result = ExpirationParser.Parse("=>024366"); // 2024 leap year
        Assert.True(result.Success, string.Join(";", result.Errors.Select(e => e.Message)));
        Assert.Equal(366, result.Value!.OrdinalDay);
    }

    // TEST-BB-1184: the ICCBBA century digit (OCD-092, decided 2026-11-16): 0 is the 2000s and 9 the 1900s, read and
    // written; any other digit is refused with what to check.
    [Fact]
    public void Expiration_CenturyDigit_ZeroIs2000s_NineIs1900s()
    {
        var label = ExpirationParser.Parse("&>0263312359");
        Assert.True(label.Success, string.Join(";", label.Errors.Select(e => e.Message)));
        Assert.Equal(new DateTime(2026, 11, 27, 23, 59, 0), label.Value!.ExpirationLocal);

        var old = ExpirationParser.Parse("=>999365");
        Assert.True(old.Success, string.Join(";", old.Errors.Select(e => e.Message)));
        Assert.Equal(1999, old.Value!.Year);

        var two = ExpirationParser.Parse("=>226331");
        Assert.False(two.Success);
        Assert.Contains("0 is the 2000s and 9 the 1900s", two.Errors.Single().Message);

        var written = ExpirationParser.FromLocalDateTime(new DateTime(2026, 11, 27, 23, 59, 0), hasExplicitTime: true);
        Assert.Equal("0263312359", written.Value!.ExpirationEncoded);
        Assert.Equal(new DateTime(2026, 11, 27, 23, 59, 0), ExpirationParser.Parse(written.Value.Sanitized).Value!.ExpirationLocal);
    }

    // TEST-BB-1184: a stored code written before the decision (century 2, or the demo's '=yyjjj') is rewritten in the
    // ICCBBA form so a label scan matches it; anything else is left.
    [Fact]
    public void RepairStoredEncoding_RewritesCenturyTwoAndTheDemoForm()
    {
        Assert.Equal("0263312359", ExpirationParser.RepairStoredEncoding("2263312359"));
        Assert.Equal("026331", ExpirationParser.RepairStoredEncoding("226331"));
        Assert.Equal("026345", ExpirationParser.RepairStoredEncoding("=26345"));
        Assert.Null(ExpirationParser.RepairStoredEncoding("026331"));
        Assert.Null(ExpirationParser.RepairStoredEncoding("2250200"));
        Assert.Null(ExpirationParser.RepairStoredEncoding(null));
    }

    [Fact]
    public void ComponentIdentity_IncludesExtendedDivision()
    {
        var id = ComponentIdentityBuilder.Build("G123417654321", "E0206000", "A01");
        Assert.Equal("G123417654321|E0206000|A01", id);
    }

    [Fact]
    public void ComponentIdentity_WithoutExtended_IsDinAndProduct()
    {
        Assert.Equal("G123417654321|E0206000", ComponentIdentityBuilder.Build("G123417654321", "E0206000"));
    }
}
