using BloodBankLIS.Application.Admin;

namespace BloodBankLIS.Application.Tests.Isbt128;

/// <summary>TEST-BB-031 — ICCBBA extract parser (synthetic T* codes only).</summary>
public class IccbbaExtractParserTests
{
    [Fact]
    public void Classify_Json_IsCatalog() =>
        Assert.Equal(IccbbaExtractKind.CatalogJson, IccbbaExtractParser.Classify("catalog.json"));

    [Fact]
    public void Classify_AboFileName_IsAbo() =>
        Assert.Equal(IccbbaExtractKind.AboRhdDelimited, IccbbaExtractParser.Classify("abo-rhd.tsv"));

    [Fact]
    public void Classify_Access_IsRejected() =>
        Assert.Equal(IccbbaExtractKind.UnsupportedNativeDatabase, IccbbaExtractParser.Classify("pdc.mdb"));

    [Fact]
    public void Parse_AccessDatabase_Fails()
    {
        var result = IccbbaExtractParser.Parse("ISBT128.accdb", "not-parsed");
        Assert.False(result.Succeeded);
        Assert.Contains("Export", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_Excel_Fails()
    {
        var result = IccbbaExtractParser.Parse("lookup.xlsx", "not-parsed");
        Assert.False(result.Succeeded);
        Assert.Equal(IccbbaExtractParser.NativeDatabaseMessage, result.Error);
    }

    [Fact]
    public void Parse_Empty_Fails()
    {
        var result = IccbbaExtractParser.Parse("products.csv", "   ");
        Assert.False(result.Succeeded);
        Assert.Contains("empty", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_MissingProductColumns_Fails()
    {
        var result = IccbbaExtractParser.Parse("products.csv", "Foo,Bar\n1,2");
        Assert.False(result.Succeeded);
        Assert.Contains("ProductDescriptionCode", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Csv_UsesHeaderAliasesAndClassMap()
    {
        var csv = File.ReadAllText(Fixture("products.csv"));
        var result = IccbbaExtractParser.Parse("products.csv", csv);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, result.ProductCodes.Count);
        Assert.Equal("T0001", result.ProductCodes[0].ProductDescriptionCode);
        Assert.Equal("RedBloodCells", result.ProductCodes[0].ComponentClass);
        Assert.Equal("Irradiated", result.ProductCodes[0].Modifier);
        Assert.Equal(new DateOnly(2024, 1, 15), result.ProductCodes[0].EffectiveDate);
        Assert.Equal("T0002", result.ProductCodes[1].ProductDescriptionCode);
        Assert.Equal("Plasma", result.ProductCodes[1].ComponentClass);
        Assert.True(result.ProductCodes[1].RequiresExtendedDivision);
        Assert.Empty(result.AboRhdCodes);
    }

    [Fact]
    public void Parse_TsvAbo_UsesHeaderAliases()
    {
        var tsv = File.ReadAllText(Fixture("abo.tsv"));
        var result = IccbbaExtractParser.Parse("facility-abo.tsv", tsv);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, result.AboRhdCodes.Count);
        Assert.Equal("T1", result.AboRhdCodes[0].Code);
        Assert.Equal("O", result.AboRhdCodes[0].Abo);
        Assert.Equal("Positive", result.AboRhdCodes[0].RhD);
        Assert.Equal("Volunteer", result.AboRhdCodes[0].CollectionType);
        Assert.Equal("synthetic", result.AboRhdCodes[0].SpecialMessage);
        Assert.Empty(result.ProductCodes);
    }

    [Fact]
    public void Parse_JsonCatalog_ReadsBothTables()
    {
        var json = File.ReadAllText(Fixture("catalog.json"));
        var result = IccbbaExtractParser.Parse("catalog.json", json);
        Assert.True(result.Succeeded, result.Error);
        Assert.Single(result.ProductCodes);
        Assert.Equal("T0003", result.ProductCodes[0].ProductDescriptionCode);
        Assert.Equal("Platelets", result.ProductCodes[0].ComponentClass);
        Assert.Equal(new DateOnly(2030, 12, 31), result.ProductCodes[0].RetiredDate);
        Assert.Single(result.AboRhdCodes);
        Assert.Equal("T3", result.AboRhdCodes[0].Code);
        Assert.Equal("synthetic", result.AboRhdCodes[0].AdditionalPhenotype);
    }

    [Fact]
    public void Parse_InvalidJson_Fails()
    {
        var result = IccbbaExtractParser.Parse("catalog.json", "{ not json");
        Assert.False(result.Succeeded);
        Assert.Contains("JSON", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapComponentClass_WellKnownPhrases()
    {
        Assert.Equal("RedBloodCells", IccbbaExtractParser.MapComponentClass("RED BLOOD CELLS"));
        Assert.Equal("Plasma", IccbbaExtractParser.MapComponentClass("Plasma"));
        Assert.Equal("Platelets", IccbbaExtractParser.MapComponentClass("PLATELETS"));
        Assert.Equal("Cryoprecipitate", IccbbaExtractParser.MapComponentClass("CRYOPRECIPITATE"));
        Assert.Equal("WholeBlood", IccbbaExtractParser.MapComponentClass("WHOLE BLOOD"));
        Assert.Equal("Other", IccbbaExtractParser.MapComponentClass("unknown class"));
    }

    [Fact]
    public void Merge_CombinesSuccessfulParts()
    {
        var products = IccbbaExtractParser.Parse("products.csv", File.ReadAllText(Fixture("products.csv")));
        var abo = IccbbaExtractParser.Parse("abo.tsv", File.ReadAllText(Fixture("abo.tsv")));
        var merged = IccbbaExtractParser.Merge([products, abo]);
        Assert.True(merged.Succeeded);
        Assert.Equal(2, merged.ProductCodes.Count);
        Assert.Equal(2, merged.AboRhdCodes.Count);
    }

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Isbt128", "Fixtures", name);
}
